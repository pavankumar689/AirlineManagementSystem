# 🐳 Veloskyra — Docker Setup Guide

## Architecture Overview

```
Browser
  │
  ├── localhost:4201 ──► passenger-portal  (Nginx + Angular)
  ├── localhost:4200 ──► admin-portal      (Nginx + Angular)
  │
  └── localhost:5049 ──► api-gateway       (Ocelot)
                           ├── /auth/...        ──► auth-service:8080
                           ├── /flights/...     ──► flight-service:8080
                           ├── /bookings/...    ──► booking-service:8080
                           ├── /payments/...    ──► payment-service:8080
                           └── /notifications/. ──► notification-service:8080

Infrastructure:
  veloskyra-sqlserver   (SQL Server 2022 Express)  localhost:1433
  veloskyra-rabbitmq    (RabbitMQ 3 + Management)  localhost:5672 / 15672
```

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running
- At least **8 GB RAM** allocated to Docker (Settings → Resources)
- Ports free: `1433, 5672, 15672, 5049, 5228, 5090, 5122, 5180, 5137, 4200, 4201`

---

## 🚀 Running Everything

Open a terminal in `d:\flight booking system\AirlineApp\` and run:

### First time (or after code changes)
```powershell
docker-compose up --build
```

### After first run (no code changes)
```powershell
docker-compose up
```

### Run in background (detached)
```powershell
docker-compose up -d --build
```

### View logs
```powershell
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f booking-service
docker-compose logs -f auth-service
```

---

## 🛑 Stopping

```powershell
# Stop all (keeps data volumes)
docker-compose down

# Stop and DELETE all data (fresh start)
docker-compose down -v
```

---

## 🌐 Access URLs (after startup)

| Service              | URL                              |
|---------------------|----------------------------------|
| 🛫 Passenger Portal  | http://localhost:4201            |
| 🔧 Admin Portal      | http://localhost:4200            |
| 🔀 API Gateway       | http://localhost:5049            |
| 🐇 RabbitMQ UI       | http://localhost:15672 (guest/guest) |

---

## 🔄 Rebuild a Single Service

```powershell
# Example: rebuild only booking-service after code changes
docker-compose up -d --build booking-service
```

---

## 🗄️ Connect to SQL Server (optional)

Use SSMS or Azure Data Studio with:
- **Server:** `localhost,1433`
- **Auth:** SQL Server Authentication
- **Login:** `sa`
- **Password:** `Veloskyra@1234`

---

## 💡 Startup Order

Docker health checks ensure services start in the correct order:

```
sqlserver & rabbitmq (health checks)
    │
    ├── auth-service
    ├── flight-service
    │
    ├── booking-service (waits for flight-service)
    ├── payment-service
    ├── notification-service
    │
    └── api-gateway
          │
          ├── passenger-portal
          └── admin-portal
```

> **First boot takes ~3-5 minutes** — SQL Server needs time to initialize before the services connect.

---

## ⚠️ Troubleshooting

### Services fail to connect to SQL Server
SQL Server takes ~30s to initialize. The health check retries 10 times. If services still fail:
```powershell
docker-compose restart auth-service booking-service flight-service payment-service notification-service
```

### Port already in use
Check which process is using the port and kill it, or change the port mapping in `docker-compose.yml`.

### Angular build fails
Ensure you have sufficient disk space (Angular build outputs can be large). Increase Docker disk limit in Docker Desktop settings.

### RabbitMQ connection refused by consumers
Services fall back gracefully — bookings still work via the synchronous HTTP path. RabbitMQ is only needed for email notifications.
