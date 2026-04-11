# AirlineApp – Low Level Design (LLD)

This document complements the HLD by detailing flows, data structures, and runtime interactions for AirlineApp. It covers use cases, components, deployment targets, key sequences, core classes, state transitions, and activities that matter for engineers and testers.

---

## 1. Use Cases

```mermaid
flowchart LR
    Passenger((Passenger))
    Admin((Administrator))
    Support((Support / Notification))
    Gateway((External Systems))

    UC1[[Browse Flights & Schedules]]
    UC2[[Book Seats]]
    UC3[[Pay via Razorpay]]
    UC4[[Manage Bookings & Cancellations]]
    UC5[[Redeem / Earn Reward Points]]
    UC6[[Subscribe to Flight Alerts]]
    UC7[[Chat with Aria]]
    UC8[[Manage Airports/Flights/Schedules]]
    UC9[[View System Records]]
    UC10[[Create Admin Accounts]]
    UC11[[Send Notifications]]
    UC12[[Monitor Flight Alerts]]
    UC13[[Process Payments]]
    UC14[[Send Emails]]
    UC15[[Provide AI Responses]]

    Passenger --> UC1
    Passenger --> UC2
    Passenger --> UC3
    Passenger --> UC4
    Passenger --> UC5
    Passenger --> UC6
    Passenger --> UC7

    Admin --> UC8
    Admin --> UC9
    Admin --> UC10

    Support --> UC11
    Support --> UC12

    Gateway --> UC13
    Gateway --> UC14
    Gateway --> UC15
```

---

## 2. Component View

```mermaid
graph LR
    subgraph UI
        PP["PassengerPortal (Angular)"]
        AP["AdminPortal (Angular)"]
        ChatWidget["Chat Widget"]
    end
    subgraph Gateway
        GW["API Gateway (Ocelot + Polly)"]
    end
    subgraph Services
        AUTH["AuthService.API"]
        FLT["FlightService.API"]
        BKG["BookingService.API"]
        PAY["PaymentService.API"]
        NOTIF["NotificationService.API"]
        CHAT["ChatbotService.API"]
    end
    subgraph Infra
        SQL["SQL Server"]
        MQ["RabbitMQ"]
        Razorpay["Razorpay"]
        SMTP["SMTP/Gmail"]
        Gemini["Gemini API"]
    end

    PP --> GW
    AP --> GW
    ChatWidget --> GW

    GW --> AUTH
    GW --> FLT
    GW --> BKG
    GW --> PAY
    GW --> NOTIF
    GW --> CHAT

    AUTH --> SQL
    FLT --> SQL
    BKG --> SQL
    PAY --> SQL
    NOTIF --> SQL

    BKG --> MQ
    PAY --> MQ
    FLT --> MQ
    NOTIF --> MQ

    PAY --> Razorpay
    NOTIF --> SMTP
    CHAT --> Gemini
```

---

## 3. Deployment View

```mermaid
graph TD
  subgraph Dev Workstation / Docker Host
    subgraph docker-compose
      GatewayPod[API Gateway Container]
      AuthPod[AuthService Container]
      FlightPod[FlightService Container]
      BookingPod[BookingService Container]
      PaymentPod[PaymentService Container]
      NotificationPod[NotificationService Container]
      ChatbotPod[ChatbotService Container]
      AngularPod[Passenger/Admin Portals via nginx]
      SQLPod[SQL Server 2022 Container]
      RabbitPod[RabbitMQ 3-management]
    end
  end

  AngularPod --> GatewayPod
  GatewayPod --> AuthPod
  GatewayPod --> FlightPod
  GatewayPod --> BookingPod
  GatewayPod --> PaymentPod
  GatewayPod --> NotificationPod
  GatewayPod --> ChatbotPod

  AuthPod --> SQLPod
  FlightPod --> SQLPod
  BookingPod --> SQLPod
  PaymentPod --> SQLPod
  NotificationPod --> SQLPod

  BookingPod --> RabbitPod
  PaymentPod --> RabbitPod
  NotificationPod --> RabbitPod
  FlightPod --> RabbitPod

  PaymentPod -->|HTTPS| Razorpay[(Razorpay SaaS)]
  NotificationPod -->|SMTP| Gmail[(SMTP Provider)]
  ChatbotPod -->|HTTPS| Gemini[(Gemini API)]
```

---

## 4. Sequence Diagrams

### 4.1 Authentication (Login + Refresh)

```mermaid
sequenceDiagram
  participant FE as PassengerPortal
  participant GW as API Gateway
  participant AUTH as Auth Service
  participant SQL as Auth DB

  FE->>GW: POST /auth/login {email,password}
  GW->>AUTH: Forward request + JWT validation rules
  AUTH->>SQL: Verify user + hash
  SQL-->>AUTH: User, password hash
  AUTH-->>GW: accessToken + refreshToken(cookie)
  GW-->>FE: 200 + JWT + HttpOnly cookie

  Note over FE: Access token stored in memory

  FE->>GW: POST /auth/refresh (with cookie)
  GW->>AUTH: Forward refresh request
  AUTH->>SQL: Validate refresh token, rotate
  SQL-->>AUTH: Token OK
  AUTH-->>GW: New access token + cookie
  GW-->>FE: 200 new JWT
```

### 4.2 Booking + Payment Saga

```mermaid
sequenceDiagram
  participant FE as PassengerPortal
  participant GW as API Gateway
  participant BKG as Booking Service
  participant PAY as Payment Service
  participant MQ as RabbitMQ
  participant Razorpay as Razorpay
  participant AUTH as Auth Service
  participant FLT as Flight Service
  participant NOTIF as Notification Service

  FE->>GW: POST /bookings {schedule, passengers}
  GW->>BKG: Forward + identity headers
  BKG->>FLT: GET schedule + seat validation
  FLT-->>BKG: Schedule data
  BKG->>BKG: Persist Pending bookings
  BKG->>MQ: Publish booking-created
  BKG-->>GW: Pending booking response
  GW-->>FE: BookingId + PNR

  FE->>GW: POST /payments/create-order
  GW->>PAY: Create Razorpay order
  PAY-->>FE: orderId + key
  FE->>Razorpay: Open checkout widget
  Razorpay-->>FE: paymentId + signature

  FE->>GW: POST /payments/verify {orderId,paymentId,...}
  GW->>PAY: Forward
  PAY->>Razorpay: Verify signature
  Razorpay-->>PAY: OK
  PAY->>PAY: Mark payment Success
  PAY->>BKG: POST /booking/{id}/confirm (sync)
  PAY->>AUTH: POST /auth/rewards/earn
  PAY->>MQ: Publish payment-completed

  MQ->>BKG: payment-completed event
  BKG->>FLT: Deduct seats (per booking)
  BKG->>MQ: Publish booking-confirmed
  MQ->>NOTIF: booking-confirmed
  NOTIF->>SMTP: Send confirmation email
```

### 4.3 Flight Status Alert

```mermaid
sequenceDiagram
  participant Admin as AdminPortal
  participant GW as API Gateway
  participant FLT as Flight Service
  participant MQ as RabbitMQ
  participant NOTIF as Notification Service
  participant SMTP as Email Server

  Admin->>GW: PATCH /flights/schedule/{id}/status {Delayed}
  GW->>FLT: UpdateStatusAsync
  FLT->>FLT: Persist new status
  FLT->>MQ: Publish flight-status-changed
  MQ->>NOTIF: Event delivered
  NOTIF->>SQL: Fetch subscribed passengers
  NOTIF->>SMTP: Send alert emails
```

---

## 5. Core Classes & Data Model

```mermaid
classDiagram
  class User {
    +int Id
    +string FullName
    +string Email
    +string PasswordHash
    +string Role
    +int RewardPoints
  }
  class RefreshToken {
    +int Id
    +string Token
    +DateTime ExpiryDate
    +bool IsRevoked
    +int UserId
  }
  class Booking {
    +int Id
    +int PassengerId
    +string PNR
    +int ScheduleId
    +string FlightNumber
    +string Class
    +string SeatNumber
    +decimal TotalAmount
    +string Status
    +DateTime CreatedAt
  }
  class Payment {
    +int Id
    +int BookingId
    +int PassengerId
    +string Method
    +decimal Amount
    +string Status
    +DateTime PaidAt
  }
  class Flight {
    +int Id
    +string FlightNumber
    +int OriginAirportId
    +int DestinationAirportId
    +int TotalEconomySeats
    +int TotalBusinessSeats
  }
  class Schedule {
    +int Id
    +int FlightId
    +DateTime DepartureTime
    +DateTime ArrivalTime
    +decimal EconomyPrice
    +decimal BusinessPrice
    +int AvailableEconomySeats
    +int AvailableBusinessSeats
    +string Status
  }
  class RewardPointsLog {
    +int Id
    +int UserId
    +int Points
    +string Type
    +string Description
    +string ReferenceId
    +DateTime CreatedAt
  }

  User "1" -- "many" RefreshToken
  User "1" -- "many" Booking : owns
  Booking "1" -- "many" Payment : references
  Flight "1" -- "many" Schedule
  Schedule "1" -- "many" Booking
  User "1" -- "many" RewardPointsLog
```

---

## 6. Booking State Diagram

```mermaid
stateDiagram-v2
  [*] --> Pending : Booking created<br/>awaiting payment
  Pending --> Confirmed : Payment success<br/>seat deducted
  Pending --> Expired : Payment timeout<br/>cleanup job
  Pending --> Cancelled : Payment failed
  Confirmed --> Cancelled : Passenger cancellation<br/>or admin action
  Cancelled --> Refunded : Rewards refunded<br/>(and payment gateway refund)
  Refunded --> [*]
  Expired --> [*]
```

---

## 7. Activity Diagram – Booking Creation & Seat Assignment

```mermaid
flowchart TD
  A[Passenger submits booking form] --> B{Auth valid?}
  B -- No --> Z[Return 401 via Gateway]
  B -- Yes --> C[BookingService fetches schedule]
  C --> D{Flight exists & seat available?}
  D -- No --> Y[Return error to UI]
  D -- Yes --> E[Generate PNR + seat numbers]
  E --> F[Persist bookings with Status=Pending]
  F --> G[Publish booking-created event]
  G --> H[Return booking summary + amount]
  H --> I[Passenger launches Razorpay checkout]
  I --> J{Payment success?}
  J -- No --> K[Notify failure -> payment-failed event -> booking cancelled]
  J -- Yes --> L[PaymentService confirm booking]
  L --> M[BookingService deduct seat + mark Confirmed]
  M --> N[Publish booking-confirmed event]
  N --> O[NotificationService emails passenger]
```

---

## 8. Detailed Component Responsibilities

| Component | Internal Structure | Key Interfaces | Notes |
|-----------|-------------------|----------------|-------|
| **AuthService** | Controllers (`AuthController`), services (`AuthServiceImpl`, `TokenService`, `AuthEmailService`), EF `AuthDbContext` | `/api/auth/*` endpoints, reward HTTP APIs for Booking/Payment, SMTP | Handles refresh-token rotation, reward ledger, admin management, forgot/reset flows |
| **FlightService** | Controllers for `Airport`, `Flight`, `Schedule`; `ScheduleServiceImpl` coordinating seat inventory; RabbitMQ publisher | `/api/airport`, `/api/flight`, `/api/schedule` plus seat adjust endpoints | Publishes `flight-status-changed`, exposes search & seat deduction APIs for booking saga |
| **BookingService** | `BookingController`, `BookingServiceImpl`, HTTP client for Flight, `RabbitMQPublisher`, background consumers (payment events, cleanup) | `/api/booking/*`, RabbitMQ queues, Auth reward refund HTTP | Implements seat locking, price logic, mass-passenger handling, occupancy query, saga orchestrator |
| **PaymentService** | `PaymentController`, `RazorpayService`, RabbitMQ publisher/consumer, `PaymentDbContext` | `/api/payment/*`, Razorpay API, Booking/Auth HTTP, RabbitMQ | Persists payment attempts, handles verification, compensates booking status |
| **NotificationService** | `AlertController`, `EmailService`, `NotificationEventConsumer`, `NotificationDbContext` | `/api/alert/*`, SMTP, RabbitMQ | Stores subscriptions, generates HTML emails, ensures retries via consumer ack |
| **ChatbotService** | FastAPI app, session store, Gemini integration helpers | `/api/chatbot/questions`, `/api/chatbot/message`, Flight/Booking HTTP, Gemini API | Collects live data, crafts prompts, enforces session caps, CORS open for frontend |
| **API Gateway** | Ocelot config, JWT middleware, RetryDelegatingHandler, NLog | `/auth`, `/flights`, `/bookings`, `/payments`, `/notifications`, `/chatbot` upstream routes | Injects `X-User-*` headers, central authorization, cross-origin control |

---

## 9. Data Persistence & Transactions

- **Transaction boundaries:** Each service keeps local transactions (EF Core `SaveChanges`). Cross-service consistency achieved via saga events; seat deduction occurs within BookingService after confirming seats via FlightService HTTP call.
- **Idempotency:** Event consumers check booking/payment status before mutating (e.g., `UpdateBookingStatusAsync` short-circuits if status unchanged). Payment verification polls DB until payment record exists to avoid race conditions.
- **Cleanup:** BookingService `BookingCleanupService` (not depicted above) scans for expired pending bookings and releases seats; NotificationService uses soft deletes for alert subscriptions.

---

## 10. Extensibility Hooks

- **Event versioning:** Shared.Events library organizes DTOs; any breaking change requires versioned event names.
- **Adapters:** Additional payment providers or notification channels can integrate by extending publisher/subscriber patterns.
- **Observability:** NLog currently outputs to files/console; instrumentation (OpenTelemetry) can be layered on with minimal disruption thanks to centralized logging bootstrapping.

