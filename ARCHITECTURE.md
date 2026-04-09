# Veloskyra Airlines — System Architecture Document

**Version:** 1.0  
**Stack:** .NET 10 · Angular 21 · SQL Server 2022 · RabbitMQ 3 · Docker  
**Pattern:** Microservices · Event-Driven · SAGA · Clean Architecture

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [High-Level Architecture](#2-high-level-architecture)
3. [Service Inventory](#3-service-inventory)
4. [API Gateway — Auth & Routing](#4-api-gateway--auth--routing)
5. [Service Deep Dives](#5-service-deep-dives)
6. [Event-Driven Communication](#6-event-driven-communication)
7. [Booking & Payment SAGA Flow](#7-booking--payment-saga-flow)
8. [Data Architecture](#8-data-architecture)
9. [Security Architecture](#9-security-architecture)
10. [Deployment Architecture](#10-deployment-architecture)
11. [Inter-Service Communication Map](#11-inter-service-communication-map)
12. [API Reference](#12-api-reference)

---

## 1. System Overview

Veloskyra Airlines is a full-stack airline booking platform built on a microservices architecture. It supports two user personas — **Passengers** who search and book flights, and **Admins/SuperAdmins** who manage the fleet and operations — each served by a dedicated Angular frontend portal.

The backend is composed of five independently deployable .NET 10 microservices, each owning its own SQL Server database. All client traffic enters through a single **Ocelot API Gateway** which handles JWT authentication centrally and forwards verified user identity to downstream services via HTTP headers.

Asynchronous workflows (booking confirmation, payment processing, email notifications) are coordinated through **RabbitMQ** using the **SAGA choreography pattern**, ensuring data consistency across service boundaries without distributed transactions.

---

## 2. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            CLIENT LAYER                                     │
│                                                                             │
│   ┌──────────────────────────┐      ┌──────────────────────────┐           │
│   │    Passenger Portal      │      │      Admin Portal        │           │
│   │   Angular 21 · :4201     │      │   Angular 21 · :4200     │           │
│   └────────────┬─────────────┘      └─────────────┬────────────┘           │
└────────────────┼────────────────────────────────────┼───────────────────────┘
                 │  HTTP/REST (withCredentials)        │
                 ▼                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          API GATEWAY  :5049                                 │
│                                                                             │
│   ┌─────────────────────────────────────────────────────────────────────┐  │
│   │  Ocelot Reverse Proxy + JWT Validation (HMAC-SHA256)                │  │
│   │                                                                     │  │
│   │  • Validates Bearer token on protected routes                       │  │
│   │  • Extracts claims → forwards as X-User-Id / X-User-Role headers   │  │
│   │  • Routes /auth/* /flights/* /bookings/* /payments/* /notifications │  │
│   │  • Aggregated Swagger UI (MMLib.SwaggerForOcelot)                  │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
└──────┬──────────────┬──────────────┬──────────────┬──────────────┬──────────┘
       │              │              │              │              │
       ▼              ▼              ▼              ▼              ▼
  ┌─────────┐   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐
  │  Auth   │   │  Flight  │  │ Booking  │  │ Payment  │  │Notification  │
  │ Service │   │ Service  │  │ Service  │  │ Service  │  │  Service     │
  │  :5228  │   │  :5090   │  │  :5122   │  │  :5180   │  │   :5137      │
  └────┬────┘   └────┬─────┘  └────┬─────┘  └────┬─────┘  └──────┬───────┘
       │              │              │              │              │
       ▼              ▼              ▼              ▼              ▼
  ┌─────────┐   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────┐
  │  SQL DB │   │  SQL DB  │  │  SQL DB  │  │  SQL DB  │  │   SQL DB     │
  │  AUTH   │   │  FLIGHT  │  │ BOOKING  │  │ PAYMENT  │  │NOTIFICATION  │
  └─────────┘   └──────────┘  └──────────┘  └──────────┘  └──────────────┘
                                    │              │
                                    └──────┬───────┘
                                           ▼
                              ┌─────────────────────────┐
                              │   RabbitMQ  :5672        │
                              │   Management UI :15672   │
                              └─────────────────────────┘
```

---

## 3. Service Inventory

| Service | Port | Database | Responsibilities |
|---|---|---|---|
| **API Gateway** | 5049 | — | JWT auth, routing, claim forwarding, Swagger aggregation |
| **AuthService** | 5228 | AIRLINE_AUTHENTICATION | Register, login, token refresh, password reset, reward points |
| **FlightService** | 5090 | AIRLINE_FLIGHT | Airports, flights, schedules, seat inventory, status events |
| **BookingService** | 5122 | AIRLINE_BOOKING | Create/cancel bookings, PNR generation, seat reservation |
| **PaymentService** | 5180 | AIRLINE_PAYMENT | Razorpay order creation, payment verification, refunds |
| **NotificationService** | 5137 | AIRLINE_NOTIFICATION | Email notifications, flight alert subscriptions |
| **Passenger Portal** | 4201 | — | Angular SPA for passengers |
| **Admin Portal** | 4200 | — | Angular SPA for admins/superadmins |
| **SQL Server** | 1433 | 5 databases | Persistent storage for all services |
| **RabbitMQ** | 5672 / 15672 | — | Async event bus between services |

---

## 4. API Gateway — Auth & Routing

The gateway is the **single entry point** for all client traffic. It is the only component that validates JWTs — downstream services trust the forwarded headers.

### Route Configuration

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        OCELOT ROUTING TABLE                                 │
├──────────────────────────┬──────────────┬──────────────┬────────────────────┤
│ Upstream Path            │ Methods      │ Auth         │ Downstream         │
├──────────────────────────┼──────────────┼──────────────┼────────────────────┤
│ /auth/{everything}       │ ALL          │ None (public)│ auth-service:8080  │
├──────────────────────────┼──────────────┼──────────────┼────────────────────┤
│ /flights/{everything}    │ GET          │ None (public)│ flight-service:8080│
│ /flights/{everything}    │ POST/PUT/    │ Bearer JWT   │ flight-service:8080│
│                          │ DELETE/PATCH │ (Admin only) │                    │
├──────────────────────────┼──────────────┼──────────────┼────────────────────┤
│ /bookings/occupied-seats │ GET          │ None (public)│ booking-service    │
│ /bookings/{everything}   │ ALL          │ Bearer JWT   │ booking-service    │
├──────────────────────────┼──────────────┼──────────────┼────────────────────┤
│ /payments/{everything}   │ GET/POST     │ Bearer JWT   │ payment-service    │
├──────────────────────────┼──────────────┼──────────────┼────────────────────┤
│ /notifications/{...}     │ ALL          │ Bearer JWT   │ notification-svc   │
└──────────────────────────┴──────────────┴──────────────┴────────────────────┘
```

### Claim Forwarding

When a JWT is validated, the gateway extracts claims and injects them as HTTP headers before forwarding:

```
JWT Claim                          →  HTTP Header
─────────────────────────────────────────────────
NameIdentifier (userId)            →  X-User-Id
Name (fullName)                    →  X-User-Name
Email                              →  X-User-Email
Role (Passenger/Admin/SuperAdmin)  →  X-User-Role
```

Downstream services read identity directly from these headers — no JWT parsing required.

---

## 5. Service Deep Dives

### 5.1 AuthService

The identity and security hub of the platform.

```
AuthService
├── Controllers
│   └── AuthController          — All auth endpoints
├── Application
│   ├── IAuthService            — Contract
│   └── DTOs                    — LoginDto, RegisterDto, RewardPointsDtos, etc.
├── Domain
│   ├── User                    — Core identity entity
│   ├── RefreshToken            — Opaque 7-day rotation token
│   ├── RewardPointsLog         — Loyalty points audit trail
│   └── PasswordResetToken      — Time-limited reset token
└── Infrastructure
    ├── AuthServiceImpl         — Business logic
    ├── TokenService            — JWT + refresh token generation
    └── AuthEmailService        — Password reset emails
```

**Token Strategy:**

```
┌──────────────────────────────────────────────────────────────────┐
│                     TOKEN LIFECYCLE                              │
│                                                                  │
│  Login Request                                                   │
│       │                                                          │
│       ▼                                                          │
│  ┌─────────────┐    Access Token (JWT, 15 min)                  │
│  │  AuthService│ ──────────────────────────────► Response Body  │
│  │             │                                                 │
│  │             │    Refresh Token (opaque, 7 days)              │
│  └─────────────┘ ──────────────────────────────► HttpOnly Cookie│
│                                                                  │
│  Token Refresh (POST /auth/refresh)                             │
│       │                                                          │
│       ▼                                                          │
│  Cookie sent automatically → old token revoked → new pair issued│
│                                                                  │
│  Logout (POST /auth/logout)                                     │
│       │                                                          │
│       ▼                                                          │
│  Refresh token revoked in DB → Cookie cleared                   │
└──────────────────────────────────────────────────────────────────┘
```

**Reward Points System:**

- Earn: 1 point per ₹10 spent (triggered by PaymentService after successful payment)
- Redeem: 1 point = ₹1 discount, capped at 60% of booking total
- Refund: Points redeemed on a cancelled booking are automatically returned
- Full audit trail in `RewardPointsLogs` table

---

### 5.2 FlightService

Manages the airline's operational data — airports, flights, and schedules.

```
FlightService
├── Controllers
│   ├── AirportController       — CRUD for airports
│   ├── FlightController        — CRUD for flights
│   └── ScheduleController      — Schedule management + seat inventory
├── Domain
│   ├── Airport                 — Code, City, Country
│   ├── Flight                  — FlightNumber, Airline, Seats config
│   └── Schedule                — DepartureTime, Prices, Available seats, Status
└── Infrastructure
    ├── Services                — AirportServiceImpl, FlightServiceImpl, ScheduleServiceImpl
    ├── DataSeeder              — Seeds airports and sample flights on startup
    └── RabbitMQPublisher       — Publishes FlightStatusChangedEvent
```

**Seat Inventory Model:**

```
Schedule
├── AvailableEconomySeats    ← decremented on booking confirm
├── AvailableBusinessSeats   ← decremented on booking confirm
├── EconomyPrice             ← base price (window seats +15%)
└── BusinessPrice            ← base price (window seats +15%)

Seat deduction flow:
  BookingService → HTTP PATCH /api/schedule/{id}/deduct-seat
  BookingService → HTTP PATCH /api/schedule/{id}/release-seat  (on cancel)
```

---

### 5.3 BookingService

Orchestrates the booking lifecycle and coordinates with Flight and Payment services.

```
BookingService
├── Controllers
│   └── BookingController       — Create, view, cancel bookings
├── Domain
│   ├── Booking                 — Full booking record with PNR
│   └── Payment                 — Navigation entity (linked to PaymentService record)
├── Infrastructure
│   ├── BookingServiceImpl      — Core booking logic, SAGA coordination
│   ├── FlightServiceClient     — HTTP client to FlightService
│   ├── BookingEventConsumer    — Listens: payment-completed, payment-failed
│   ├── RabbitMQPublisher       — Publishes: booking-created, booking-confirmed, booking-cancelled
│   └── BookingCleanupService   — Background job: auto-cancels stale pending bookings (>15 min)
```

**Booking Status Lifecycle:**

```
  [Passenger submits booking]
           │
           ▼
        Pending  ──────────────────────────────────────────────────────┐
           │                                                            │
           │  BookingCreatedEvent published to RabbitMQ                │
           ▼                                                            │
  [PaymentService creates payment record]                              │
           │                                                            │
           │  Passenger completes Razorpay payment                     │
           ▼                                                            │
  [PaymentService verifies → PaymentCompletedEvent]                   │
           │                                                            │
           ▼                                                            │
       Confirmed  ◄──────────────────────────────────────────────────  │
           │                                                            │
           │  [Passenger cancels]                                       │
           ▼                                                            │
       Cancelled  ◄──────────────────────────────────────────────────  │
                                                                        │
                    [15 min timeout — no payment]  ────────────────────┘
                              Auto-cancelled by BookingCleanupService
```

**Group Booking (Multi-Passenger):**

All passengers in a group share a single PNR. The total amount is billed as one Razorpay order. On confirmation, each passenger's booking record is updated and a seat is deducted per passenger.

---

### 5.4 PaymentService

Integrates with Razorpay for real payment processing.

```
PaymentService
├── Controllers
│   └── PaymentController       — Create order, verify payment, view history
├── Domain
│   └── Payment                 — Full payment record with passenger details
├── Infrastructure
│   ├── PaymentServiceImpl      — Query payments
│   ├── RazorpayService         — Order creation + HMAC-SHA256 signature verification
│   ├── PaymentEventConsumer    — Listens: booking-created → creates payment record
│   └── RabbitMQPublisher       — Publishes: payment-completed
```

**Razorpay Payment Flow:**

```
  Frontend                  PaymentService              Razorpay
     │                           │                          │
     │  POST /payments/create-order                         │
     │ ─────────────────────────►│                          │
     │                           │  Create Order API ──────►│
     │                           │◄────────── orderId ──────│
     │◄── { orderId, keyId } ────│                          │
     │                                                       │
     │  [Razorpay checkout modal opens in browser]          │
     │ ─────────────────────────────────────────────────────►│
     │◄──────────── { paymentId, signature } ───────────────│
     │                                                       │
     │  POST /payments/verify { orderId, paymentId, sig }   │
     │ ─────────────────────────►│                          │
     │                           │  Verify HMAC signature   │
     │                           │  Update payment → Success│
     │                           │  HTTP → BookingService confirm
     │                           │  HTTP → AuthService earn points
     │                           │  Publish payment-completed
     │◄── { message: verified } ─│
```

---

### 5.5 NotificationService

Handles all outbound communications — booking emails and flight status alerts.

```
NotificationService
├── Controllers
│   └── AlertController         — Subscribe/unsubscribe to flight alerts
├── Domain
│   ├── FlightAlert             — Passenger subscription to a schedule
│   └── NotificationLog         — Email delivery audit log
└── Infrastructure
    ├── EmailService            — SMTP via Gmail + QuestPDF boarding pass generation
    └── NotificationEventConsumer — Listens: booking-confirmed, booking-cancelled,
                                              flight-status-changed
```

**Email Triggers:**

| Event | Email Sent |
|---|---|
| `booking-confirmed` | Booking confirmation + PDF boarding pass |
| `booking-cancelled` | Cancellation notice with refund amount |
| `flight-status-changed` | Alert to all subscribed passengers |

---

## 6. Event-Driven Communication

All asynchronous communication uses RabbitMQ with durable queues (messages survive broker restarts). Each consumer implements a 10-attempt retry loop with 5-second delays to handle startup race conditions.

### Message Bus Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         RABBITMQ MESSAGE FLOWS                              │
│                                                                             │
│                                                                             │
│  BookingService ──── booking-created ────────────────► PaymentService      │
│                                                                             │
│  PaymentService ──── payment-completed ──────────────► BookingService      │
│                                                                             │
│  BookingService ──── booking-confirmed ──────────────► NotificationService │
│                                                                             │
│  BookingService ──── booking-cancelled ──────────────► NotificationService │
│                                                                             │
│  FlightService  ──── flight-status-changed ──────────► NotificationService │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Event Schemas

**booking-created** (BookingService → PaymentService)
```
BookingCreatedEvent {
  BookingId, PassengerId, PassengerName, PassengerEmail,
  FlightNumber, Origin, Destination, DepartureTime,
  Class, SeatNumber, Amount, PaymentMethod, ScheduleId
}
```

**payment-completed** (PaymentService → BookingService)
```
PaymentCompletedEvent {
  BookingId, PassengerId, PassengerEmail, PassengerName,
  FlightNumber, Origin, Destination, DepartureTime,
  SeatNumber, Amount, ScheduleId, Class
}
```

**booking-confirmed** (BookingService → NotificationService)
```
BookingConfirmedEvent {
  BookingId, PNR, PassengerEmail, PassengerName,
  FlightNumber, Origin, Destination, DepartureTime,
  SeatNumber, Amount, Class
}
```

**booking-cancelled** (BookingService → NotificationService)
```
BookingCancelledEvent {
  BookingId, PassengerEmail, PassengerName,
  FlightNumber, RefundAmount
}
```

**flight-status-changed** (FlightService → NotificationService)
```
FlightStatusChangedEvent {
  ScheduleId, FlightNumber, Origin, Destination,
  DepartureTime, OldStatus, NewStatus
}
```

---

## 7. Booking & Payment SAGA Flow

The booking-to-payment workflow uses the **SAGA choreography pattern** — no central orchestrator, each service reacts to events and publishes its own.

```
Passenger          Gateway         BookingService      PaymentService    NotificationService
    │                 │                  │                   │                  │
    │  POST /bookings │                  │                   │                  │
    │────────────────►│                  │                   │                  │
    │                 │  JWT validated   │                   │                  │
    │                 │─────────────────►│                   │                  │
    │                 │                  │ Validate seats    │                  │
    │                 │                  │ Create Booking    │                  │
    │                 │                  │ Status=Pending    │                  │
    │                 │                  │                   │                  │
    │                 │                  │──booking-created─►│                  │
    │                 │                  │                   │ Create Payment   │
    │                 │                  │                   │ Status=Processing│
    │◄────────────────│◄─ BookingId ─────│                   │                  │
    │                 │                  │                   │                  │
    │  POST /payments/create-order       │                   │                  │
    │────────────────────────────────────────────────────────►                  │
    │◄──────────────────────────── orderId, keyId ───────────│                  │
    │                                                         │                  │
    │  [Razorpay modal — user pays]                          │                  │
    │                                                         │                  │
    │  POST /payments/verify                                  │                  │
    │────────────────────────────────────────────────────────►│                  │
    │                 │                  │                   │ Verify HMAC sig  │
    │                 │                  │                   │ Payment=Success  │
    │                 │                  │◄── HTTP confirm ──│                  │
    │                 │                  │ Status=Confirmed  │                  │
    │                 │                  │ Deduct seat       │                  │
    │                 │                  │                   │──payment-completed►
    │                 │                  │◄──────────────────│                  │
    │                 │                  │ booking-confirmed─────────────────────►
    │                 │                  │                   │                  │ Send email
    │◄──────────────── verified ─────────────────────────────│                  │ + boarding pass
    │                 │                  │                   │                  │
    │                 │                  │ HTTP → AuthService /rewards/earn     │
    │                 │                  │ (1 pt per ₹10 paid)                  │
```

### Cancellation Flow

```
Passenger          BookingService      FlightService     AuthService    NotificationService
    │                  │                   │                 │                 │
    │  POST /bookings/{id}/cancel          │                 │                 │
    │─────────────────►│                   │                 │                 │
    │                  │ Status=Cancelled  │                 │                 │
    │                  │ Payment=Refunded  │                 │                 │
    │                  │──── HTTP release-seat ─────────────►│                 │
    │                  │──── HTTP /rewards/refund ───────────►│                 │
    │                  │──── booking-cancelled ──────────────────────────────►│
    │◄─ cancelled ─────│                   │                 │                 │ Send email
```

---

## 8. Data Architecture

Each microservice owns its own isolated SQL Server database. There are no cross-database joins — services communicate via APIs and events.

### Database per Service

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        DATABASE ISOLATION                                   │
│                                                                             │
│  AIRLINE_AUTHENTICATION          AIRLINE_FLIGHT                            │
│  ┌──────────────────────┐        ┌──────────────────────┐                  │
│  │ Users                │        │ Airports             │                  │
│  │ RefreshTokens        │        │ Flights              │                  │
│  │ PasswordResetTokens  │        │ Schedules            │                  │
│  │ RewardPointsLogs     │        └──────────────────────┘                  │
│  └──────────────────────┘                                                  │
│                                                                             │
│  AIRLINE_BOOKING                 AIRLINE_PAYMENT                           │
│  ┌──────────────────────┐        ┌──────────────────────┐                  │
│  │ Bookings             │        │ Payments             │                  │
│  │ Payments (nav only)  │        └──────────────────────┘                  │
│  └──────────────────────┘                                                  │
│                                                                             │
│  AIRLINE_NOTIFICATION                                                       │
│  ┌──────────────────────┐                                                  │
│  │ FlightAlerts         │                                                  │
│  │ NotificationLogs     │                                                  │
│  └──────────────────────┘                                                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Key Entity Relationships

```
AIRLINE_AUTHENTICATION
  Users (Id, FullName, Email, PasswordHash, Role, RewardPoints)
    │
    ├──► RefreshTokens (Token, UserId, ExpiryDate, IsRevoked)
    ├──► PasswordResetTokens (Token, UserId, ExpiryDate, IsUsed)
    └──► RewardPointsLogs (UserId, Points, Type, Description, ReferenceId)

AIRLINE_FLIGHT
  Airports (Id, Name, Code, City, Country)
    │
  Flights (Id, FlightNumber, Airline, OriginAirportId, DestinationAirportId,
           TotalEconomySeats, TotalBusinessSeats)
    │
  Schedules (Id, FlightId, DepartureTime, ArrivalTime, EconomyPrice,
             BusinessPrice, AvailableEconomySeats, AvailableBusinessSeats, Status)

AIRLINE_BOOKING
  Bookings (Id, PassengerId, PassengerName, PassengerEmail, ScheduleId,
            FlightNumber, Origin, Destination, DepartureTime, Class,
            SeatNumber, TotalAmount, Status, PNR, CreatedAt)
    │
  Payments (Id, BookingId, Amount, Method, Status, PaidAt)  ← navigation only

AIRLINE_PAYMENT
  Payments (Id, BookingId, PassengerId, PassengerEmail, PassengerName,
            Amount, Method, Status, FlightNumber, Origin, Destination,
            SeatNumber, Class, ScheduleId, CreatedAt, PaidAt, FailureReason)

AIRLINE_NOTIFICATION
  FlightAlerts (Id, PassengerId, PassengerEmail, PassengerName,
                ScheduleId, FlightNumber, Origin, Destination,
                DepartureTime, IsActive, CreatedAt)
  NotificationLogs (Id, ToEmail, Subject, Type, IsSuccess, ErrorMessage, SentAt)
```

---

## 9. Security Architecture

### Authentication & Authorization Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        SECURITY LAYERS                                      │
│                                                                             │
│  Layer 1 — Transport                                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  CORS policy: only localhost:4200 and localhost:4201 allowed        │   │
│  │  HttpOnly cookies: refresh token never accessible to JavaScript     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  Layer 2 — Gateway (JWT Validation)                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Algorithm: HMAC-SHA256                                             │   │
│  │  Access token lifetime: 15 minutes                                  │   │
│  │  Clock skew tolerance: 1 minute                                     │   │
│  │  Validates: Issuer, Audience, Lifetime, Signature                   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  Layer 3 — Role-Based Access                                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Passenger  — book flights, view own bookings, manage profile       │   │
│  │  Admin      — manage flights, schedules, airports, view all data    │   │
│  │  SuperAdmin — all Admin rights + create/delete admin accounts       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  Layer 4 — Password Security                                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  BCrypt hashing (work factor default)                               │   │
│  │  Password reset: cryptographically random token, hashed in DB      │   │
│  │  Reset link expires in 1 hour, single-use                          │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Refresh Token Rotation

```
  Request 1: Login
  ─────────────────
  Client ──► POST /auth/login
  Server ──► Access Token (body) + Refresh Token A (HttpOnly cookie)
  DB: RefreshToken A stored, IsRevoked=false

  Request 2: Token Refresh
  ─────────────────────────
  Client ──► POST /auth/refresh  (cookie sent automatically)
  Server ──► Revoke Token A in DB
         ──► Issue Access Token (new) + Refresh Token B (new cookie)
  DB: Token A IsRevoked=true, Token B stored

  Attack Scenario: Stolen Token A used after rotation
  ─────────────────────────────────────────────────────
  Attacker ──► POST /auth/refresh with Token A
  Server   ──► Token A is revoked → 401 Unauthorized
```

### Role Access Matrix

| Endpoint | Passenger | Admin | SuperAdmin |
|---|:---:|:---:|:---:|
| Register / Login / Refresh | ✓ | ✓ | ✓ |
| View/Update own profile | ✓ | ✓ | ✓ |
| Search flights / View schedules | ✓ | ✓ | ✓ |
| Create booking | ✓ | — | — |
| View own bookings | ✓ | — | — |
| Cancel own booking | ✓ | — | — |
| View own payments | ✓ | — | — |
| Subscribe to flight alerts | ✓ | — | — |
| Manage flights / schedules | — | ✓ | ✓ |
| Manage airports | — | ✓ | ✓ |
| View all bookings / payments | — | ✓ | ✓ |
| Create admin accounts | — | — | ✓ |
| Delete user accounts | — | — | ✓ |

---

## 10. Deployment Architecture

The entire platform runs as Docker containers on a shared bridge network (`veloskyra-net`). Services communicate using Docker service names as hostnames.

### Container Topology

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    DOCKER COMPOSE NETWORK: veloskyra-net                    │
│                                                                             │
│  ┌──────────────────┐  ┌──────────────────┐                                │
│  │ veloskyra-admin  │  │veloskyra-passenger│                               │
│  │ nginx · :4200    │  │ nginx · :4201     │                               │
│  └────────┬─────────┘  └────────┬──────────┘                               │
│           └──────────┬──────────┘                                           │
│                      ▼                                                      │
│           ┌──────────────────────┐                                          │
│           │  veloskyra-gateway   │                                          │
│           │  dotnet · :5049      │                                          │
│           └──┬───┬───┬───┬───┬──┘                                          │
│              │   │   │   │   │                                              │
│    ┌─────────┘   │   │   │   └──────────────┐                              │
│    ▼             ▼   ▼   ▼                  ▼                              │
│ ┌──────┐  ┌──────┐ ┌──────┐ ┌──────┐  ┌──────────────┐                   │
│ │auth  │  │flight│ │book  │ │pay   │  │notification  │                   │
│ │:5228 │  │:5090 │ │:5122 │ │:5180 │  │:5137         │                   │
│ └──┬───┘  └──┬───┘ └──┬───┘ └──┬───┘  └──────┬───────┘                   │
│    │         │         │         │             │                            │
│    └────┬────┘         └────┬────┘             │                            │
│         ▼                   ▼                  ▼                            │
│  ┌─────────────┐    ┌──────────────────────────────┐                       │
│  │  sqlserver  │    │         rabbitmq              │                       │
│  │  :1433      │    │  AMQP:5672 · UI:15672         │                       │
│  └─────────────┘    └──────────────────────────────┘                       │
│                                                                             │
│  Volumes: sqlserver_data (persistent), rabbitmq_data (persistent)          │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Service Startup Order

```
sqlserver ──► rabbitmq ──► auth-service
                       ──► flight-service
                       ──► booking-service (waits for flight-service)
                       ──► payment-service
                       ──► notification-service
                                 └──► api-gateway
                                           └──► passenger-portal
                                           └──► admin-portal
```

### Health Checks

| Service | Health Check | Interval | Retries |
|---|---|---|---|
| SQL Server | `sqlcmd SELECT 1` | 15s | 10 |
| RabbitMQ | `rabbitmq-diagnostics ping` | 15s | 10 |

### Port Mapping

| Container | Internal Port | Host Port | Purpose |
|---|---|---|---|
| veloskyra-gateway | 8080 | 5049 | API entry point |
| veloskyra-auth | 8080 | 5228 | Auth service |
| veloskyra-flight | 8080 | 5090 | Flight service |
| veloskyra-booking | 8080 | 5122 | Booking service |
| veloskyra-payment | 8080 | 5180 | Payment service |
| veloskyra-notification | 8080 | 5137 | Notification service |
| veloskyra-passenger-portal | 80 | 4201 | Passenger Angular app |
| veloskyra-admin-portal | 80 | 4200 | Admin Angular app |
| veloskyra-sqlserver | 1433 | 1433 | SQL Server |
| veloskyra-rabbitmq | 5672/15672 | 5672/15672 | RabbitMQ |

---

## 11. Inter-Service Communication Map

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                   COMPLETE COMMUNICATION MAP                                │
│                                                                             │
│  ── Synchronous HTTP ──    ══ Async RabbitMQ ══                            │
│                                                                             │
│                                                                             │
│  BookingService ──── HTTP GET /api/schedule/{id} ──────────► FlightService │
│  BookingService ──── HTTP PATCH /deduct-seat ──────────────► FlightService │
│  BookingService ──── HTTP PATCH /release-seat ─────────────► FlightService │
│                                                                             │
│  PaymentService ──── HTTP POST /api/booking/{id}/confirm ──► BookingService│
│  PaymentService ──── HTTP POST /api/auth/rewards/earn ─────► AuthService   │
│                                                                             │
│  BookingService ──── HTTP POST /api/auth/rewards/refund ───► AuthService   │
│                                                                             │
│  BookingService ════ booking-created ══════════════════════► PaymentService│
│  PaymentService ════ payment-completed ════════════════════► BookingService│
│  BookingService ════ booking-confirmed ════════════════════► NotifService  │
│  BookingService ════ booking-cancelled ════════════════════► NotifService  │
│  FlightService  ════ flight-status-changed ════════════════► NotifService  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Clean Architecture Layer Structure (per service)

```
  ┌─────────────────────────────────────────────────────┐
  │                    API Layer                        │
  │  Controllers · Program.cs · Swagger/Scalar UI       │
  ├─────────────────────────────────────────────────────┤
  │                Application Layer                    │
  │  Interfaces (contracts) · DTOs                      │
  ├─────────────────────────────────────────────────────┤
  │                  Domain Layer                       │
  │  Entities (pure C# classes, no framework deps)      │
  ├─────────────────────────────────────────────────────┤
  │               Infrastructure Layer                  │
  │  EF Core DbContext · Service Implementations        │
  │  RabbitMQ Publishers/Consumers · HTTP Clients       │
  └─────────────────────────────────────────────────────┘
  Dependency rule: outer layers depend on inner layers only
```

---

## 12. API Reference

### AuthService — `/auth/*`

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/auth/api/auth/register` | Public | Register new passenger |
| POST | `/auth/api/auth/login` | Public | Login, returns JWT + sets cookie |
| POST | `/auth/api/auth/refresh` | Cookie | Rotate refresh token |
| POST | `/auth/api/auth/logout` | Cookie | Revoke token + clear cookie |
| GET | `/auth/api/auth/profile` | JWT | Get own profile |
| PUT | `/auth/api/auth/profile` | JWT | Update own profile |
| POST | `/auth/api/auth/change-password` | JWT | Change password |
| POST | `/auth/api/auth/forgot-password` | Public | Send reset email |
| POST | `/auth/api/auth/reset-password` | Public | Reset with token |
| GET | `/auth/api/auth/rewards` | Passenger | View points balance + history |
| POST | `/auth/api/auth/rewards/earn` | Internal | Award points after payment |
| POST | `/auth/api/auth/rewards/redeem` | Passenger | Redeem points for discount |
| POST | `/auth/api/auth/rewards/refund` | Internal | Refund points on cancellation |
| POST | `/auth/api/auth/create-admin` | SuperAdmin | Create admin account |
| GET | `/auth/api/auth/users` | SuperAdmin | List all users |
| DELETE | `/auth/api/auth/users/{id}` | SuperAdmin | Delete user |

### FlightService — `/flights/*`

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/flights/api/airport` | Public | List all airports |
| POST | `/flights/api/airport` | Admin | Create airport |
| DELETE | `/flights/api/airport/{id}` | Admin | Delete airport |
| GET | `/flights/api/flight` | Public | List all flights |
| POST | `/flights/api/flight` | Admin | Create flight |
| PUT | `/flights/api/flight/{id}` | Admin | Update flight |
| DELETE | `/flights/api/flight/{id}` | Admin | Delete flight |
| GET | `/flights/api/schedule` | Public | List all schedules |
| GET | `/flights/api/schedule/search` | Public | Search by origin/destination/date |
| GET | `/flights/api/schedule/{id}` | Public | Get schedule by ID |
| POST | `/flights/api/schedule` | Admin | Create schedule |
| PATCH | `/flights/api/schedule/{id}/status` | Admin | Update flight status |

### BookingService — `/bookings/*`

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/bookings/api/booking` | Passenger | Create booking (single or group) |
| GET | `/bookings/api/booking/my-bookings` | Passenger | View own bookings |
| GET | `/bookings/api/booking/{id}` | Passenger | View specific booking |
| POST | `/bookings/api/booking/{id}/cancel` | Passenger | Cancel booking |
| GET | `/bookings/api/booking/all` | Admin | View all bookings |
| GET | `/bookings/api/booking/occupied-seats/{scheduleId}` | Public | Get occupied seats |

### PaymentService — `/payments/*`

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/payments/api/payment` | Admin | View all payments |
| GET | `/payments/api/payment/my-payments` | Passenger | View own payments |
| POST | `/payments/api/payment/create-order` | Passenger | Create Razorpay order |
| POST | `/payments/api/payment/verify` | Passenger | Verify Razorpay payment |

### NotificationService — `/notifications/*`

| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/notifications/api/alert/subscribe` | Passenger | Subscribe to flight alerts |
| GET | `/notifications/api/alert/my-alerts` | Passenger | View active subscriptions |
| DELETE | `/notifications/api/alert/{id}` | Passenger | Unsubscribe from alert |

---

## Developer Access Points

| Resource | URL |
|---|---|
| Passenger Portal | http://localhost:4201 |
| Admin Portal | http://localhost:4200 |
| Aggregated Swagger UI (Gateway) | http://localhost:5049/swagger |
| RabbitMQ Management UI | http://localhost:15672 (guest/guest) |
| Auth Service Scalar UI | http://localhost:5228/scalar/v1 |
| Flight Service Scalar UI | http://localhost:5090/scalar/v1 |
| Booking Service Scalar UI | http://localhost:5122/scalar/v1 |
| Payment Service Scalar UI | http://localhost:5180/scalar/v1 |
| Notification Service Scalar UI | http://localhost:5137/scalar/v1 |

---

*Document generated from source code analysis — reflects the actual running system.*
