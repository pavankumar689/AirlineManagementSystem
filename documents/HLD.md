# AirlineApp – High-Level Design (HLD)

## 1. Purpose
This document summarizes the end-to-end design of the AirlineApp platform so engineers, testers, and operators share a common understanding before implementation or major changes. It focuses on service boundaries, integration patterns, deployment topology, and key non-functional expectations rather than low-level API or schema details.

## 2. System Context
- **Primary actors:** Passengers (self-service bookings), Administrators (flight/airport management), Support/Notification staff, and automated payment/notification providers (Razorpay, SMTP).
- **Entry points:** Angular PassengerPortal and AdminPortal SPAs plus future REST/automation clients communicate through a single Ocelot-based API Gateway.
- **External systems:** Razorpay payment gateway, SMTP provider (Gmail in dev), Gemini API for chatbot, potential analytics/monitoring backends.

## 3. Architecture Overview
The solution follows a microservices style with clear domain ownership per service, asynchronous messaging for the booking/payment saga, and a shared infrastructure baseline (SQL Server, RabbitMQ).

```mermaid
flowchart LR
subgraph Clients
    PP[PassengerPortal SPA]
    AP[AdminPortal SPA]
    BotUI[Chatbot Widget]
end

GW[API Gateway (Ocelot)]
AUTH[Auth Service]
FLT[Flight Service]
BKG[Booking Service]
PAY[Payment Service]
NOTIF[Notification Service]
CHAT[Chatbot Service]

subgraph Infra
    SQL[(SQL Server)]
    MQ[(RabbitMQ)]
    Razorpay[(Razorpay)]
    SMTP[(SMTP/Gmail)]
    Gemini[(Gemini API)]
end

PP --> GW
AP --> GW
BotUI --> GW

GW --> AUTH
GW --> FLT
GW --> BKG
GW --> PAY
GW --> NOTIF
GW --> CHAT

AUTH <-->|JWT Validations| GW
AUTH --> SQL
FLT --> SQL
BKG --> SQL
PAY --> SQL
NOTIF --> SQL

BKG -->|booking-created| MQ
PAY -->|payment-completed<br/>payment-failed| MQ
FLT -->|flight-status-changed| MQ
MQ --> NOTIF
MQ --> BKG
MQ --> PAY

PAY --> Razorpay
NOTIF --> SMTP
CHAT --> Gemini
```

### Design Tenets
1. **Gateway-first security:** The API Gateway validates JWTs, injects identity headers, applies request throttling/CORS, and shields downstream services from direct public exposure.
2. **Autonomous services:** Each microservice owns its database (logical schema per service on a shared SQL Server instance) and domain logic, exposing REST APIs that reflect bounded contexts.
3. **Saga orchestration via RabbitMQ:** Booking/Payment/Notification services integrate through domain events (`booking-created`, `payment-completed`, etc.) to provide eventual consistency, retryability, and decoupling.
4. **Polyglot but standardized:** Core services run on ASP.NET Core, while the AI chatbot runs on FastAPI/Python, both packaged with Docker for consistent deployments.

## 4. Service Responsibilities
| Service | Domain Ownership | Key Responsibilities | Integrations |
|---------|------------------|----------------------|--------------|
| **API Gateway (Ocelot + Polly)** | Edge security/routing | JWT validation, header enrichment, resilient downstream routing (Polly retries), CORS | All backend services |
| **Auth Service** | Identity, sessions, reward ledger | Register/login/logout/refresh, user admin, password resets, reward earn/redeem/refund, refresh-token rotation | SQL (Auth DB), Email, Booking/Payment via HTTP |
| **Flight Service** | Airports, flights, schedules, seats | CRUD for airports/flights/schedules, seat inventory management, schedule search, flight status changes | SQL (Flight DB), RabbitMQ (`flight-status-changed`), Booking HTTP client |
| **Booking Service** | Passenger bookings & seat reservations | Booking creation, seat selection/locking, booking queries, cancellations, saga initiation (`booking-created`), compensation, seat synchronization | SQL (Booking DB), RabbitMQ, Flight HTTP client, Auth HTTP for rewards |
| **Payment Service** | Payments & Razorpay orchestration | Create Razorpay orders, verify signatures, persist payments, confirm bookings, award rewards, emit `payment-*` events | SQL (Payment DB), Razorpay, Booking/Auth HTTP, RabbitMQ |
| **Notification Service** | Alerts & messaging | Passenger alert subscriptions, booking/flight notifications, email delivery via MailKit, consumes messaging queues | SQL (Notification DB), RabbitMQ, SMTP |
| **Chatbot Service (Aria)** | Conversational assistance | Fetch live flight/booking context, build Gemini prompts, maintain session memory, expose chatbot REST endpoints | Flight/Booking HTTP, Gemini API |
| **Shared.Events** | Contract library | Domain event DTOs shared between publishers/consumers | Consumed by .NET services |

## 5. Data & Integration Model
- **Datastores:** Single SQL Server container with databases: `AIRLINE_AUTHENTICATION`, `AIRLINE_FLIGHT`, `AIRLINE_BOOKING`, `AIRLINE_PAYMENT`, `AIRLINE_NOTIFICATION`. Each service uses EF Core migrations/`EnsureCreated`.
- **Messaging:** RabbitMQ hosts durable queues (`booking-created`, `payment-completed`, `payment-failed`, `booking-confirmed`, `booking-cancelled`, `flight-status-changed`). Consumers run as hosted background services with retry/backoff logic for broker connectivity.
- **HTTP flows:** Inter-service REST calls are limited (Booking→Flight for seat updates, Payment→Booking/Auth for confirmation and reward points, Booking→Auth for reward refunds). The gateway injects `X-User-*` headers so downstream controllers operate without parsing JWTs.
- **Secrets/config:** Provided via environment variables (Docker) for DB strings, RabbitMQ host, JWT secrets, Razorpay keys, SMTP credentials, Gemini API key.

## 6. Critical Flows
1. **Authentication & Session Renewal**
   - User hits `/auth/login` through gateway; AuthService verifies and returns access token + sets HttpOnly refresh token cookie.
   - SPAs store access token in memory; interceptors call `/auth/refresh` with cookies to rotate tokens.
2. **Booking + Payment Saga**
   - PassengerPortal posts booking request; BookingService validates schedule/seats, stores pending records, emits `booking-created`.
   - PaymentService listens, creates a payment row, then Razorpay checkout occurs via SPA.
   - On verification, PaymentService confirms booking (HTTP), awards reward points (Auth), emits `payment-completed`.
   - BookingEventConsumer processes the event, sets status to Confirmed, publishes `booking-confirmed` for NotificationService.
   - Failures trigger `payment-failed` → booking cancellation + `booking-cancelled`.
3. **Cancellation & Reward Refund**
   - Passenger cancels via BookingService; seats released, rewards refunded via Auth, `booking-cancelled` event triggers emails.
4. **Flight Alerts**
   - Passengers subscribe with schedule metadata; FlightService status updates publish `flight-status-changed`; NotificationService emails all subscribers.
5. **Chatbot Responses**
   - Frontend sends chat message (with Authorization header if logged in); Chatbot service fetches live schedules & bookings, crafts Gemini prompt, streams response back with session tracking.

## 7. Deployment & Operations
- **Containers:** `docker-compose.yml` orchestrates SQL Server, RabbitMQ, gateway, .NET APIs, Angular builds (served via nginx), and chatbot container. Environment parity between dev/test achieved through shared compose file.
- **Logging & Monitoring:** All .NET services use NLog with configurable sinks (console, files); background services emit connection-health logs. RabbitMQ/SQL health checks defined at compose level. Future observability targets include centralized log aggregation and queue depth metrics.
- **Security:** JWT secret managed centrally by gateway and AuthService, HTTPS termination expected at ingress (not shown). Refresh tokens stored HttpOnly/SameSite. Admin APIs protected by SuperAdmin role enforced via gateway policies.
- **Resiliency:** Polly retry handler in gateway, consumer retry loops for RabbitMQ connections, saga design ensures idempotency (booking status checks, seat rollback logic), seat inventory safeguarded through `DeductSeatAsync`/`ReleaseSeatAsync` operations.
- **Scalability:** Stateless services packaged individually, allowing horizontal scaling; RabbitMQ handles bursty workloads; SQL Server remains single instance (can be replaced with managed instance if needed).

## 8. Non-Functional Considerations
- **Performance:** Flight search and booking endpoints optimized with server-side filtering and seat availability checks. Payment verification uses polling fallback to bridge potential race with `booking-created` persistence.
- **Security/Compliance:** PIIs stored only in necessary services (Auth, Booking, Payment, Notification). TLS termination, secrets vaulting, and audit logging should be hardened for production.
- **Availability:** Gateway + services should run behind load balancers; RabbitMQ and SQL Server need HA in prod (clustered/MaaS). Saga ensures no single point of failure cascades silently.
- **Extensibility:** Shared event contracts and HTTP APIs enable integrating additional services (e.g., Loyalty, Analytics) without modifying existing flows.

## 9. Open Questions / Future Enhancements
1. Replace ad-hoc reward HTTP calls with events for better decoupling?
2. Introduce Redis for chatbot sessions and seat lock caching?
3. Observability stack (OpenTelemetry, centralized dashboards) not yet defined.
4. Automated failover for SQL/RabbitMQ to meet higher SLAs.
