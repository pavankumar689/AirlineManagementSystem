# ✈️ Veloskyra Chatbot Microservice — "Aria"

> **Language:** Python 3.12 · **Framework:** FastAPI · **AI:** Google Gemini 2.5 Flash  
> **Port:** `8000` (internal) · `8100` (host, via Docker)  
> **API Gateway path:** `/chatbot/*`

---

## Why a separate Python microservice?

This service exists as a **standalone Python microservice** alongside the ASP.NET Core backend — a deliberate architectural decision, not an accident.

| Benefit | Detail |
|---|---|
| **Best-fit language** | Python's AI/ML ecosystem (google-generativeai, LangChain, HuggingFace) is unmatched. Using C# here would mean far fewer libraries and more boilerplate. |
| **Independent deployment** | Aria can be updated, rolled back, or redeployed without rebuilding any of the five ASP.NET Core services. |
| **Independent scaling** | Chat traffic spikes differently from booking/payment traffic. Kubernetes can scale this pod (or its replica count in Compose) separately. |
| **Fault isolation** | A crash or hang in the chatbot cannot propagate to the booking or payment services. The rest of the platform stays up. |
| **Team autonomy** | A Python/ML team can own this service entirely, with its own CI/CD pipeline, code reviews, and release cadence. |
| **API key security** | The Gemini API key lives server-side in an env var — it is **never exposed to the browser**, unlike a pure frontend implementation. |

---

## Architecture

```
Angular Passenger Portal
        │
        │  POST /chatbot/api/chatbot/message
        │  (JWT Bearer + X-Passenger-Name header)
        ▼
Ocelot API Gateway  (C# / ASP.NET Core — port 5049)
        │
        │  /chatbot/* → chatbot-service:8000
        ▼
┌──────────────────────────────────────────────┐
│          ChatbotService  (this repo)         │
│          Python 3.12 · FastAPI · Uvicorn     │
│                                              │
│  main.py              App factory + CORS     │
│  app/routes.py        Thin HTTP layer        │
│  app/context_fetcher.py  Calls other svcs   │
│  app/prompt_builder.py   LLM system prompt  │
│  app/gemini_client.py    Gemini SDK wrapper │
│  app/session_store.py    Conversation hist  │
│  app/models.py           Pydantic schemas   │
│  app/config.py           Env-var settings   │
└──────────────────────────────────────────────┘
        │                         │
        ▼                         ▼
 Flight Service           Booking Service
 (ASP.NET Core)           (ASP.NET Core)
 /api/schedule            /api/booking/my-bookings
```

---

## Module responsibilities

| File | Responsibility |
|---|---|
| `main.py` | FastAPI app factory, CORS middleware, router registration, startup log |
| `app/config.py` | Single place for all env vars — nothing else calls `os.getenv()` |
| `app/models.py` | Pydantic request/response schemas with OpenAPI examples |
| `app/session_store.py` | In-memory chat history (swap to Redis with zero route changes) |
| `app/context_fetcher.py` | Async HTTP calls to Flight & Booking services; runs **concurrently** via `asyncio.gather` |
| `app/prompt_builder.py` | Assembles the Gemini system prompt from live context |
| `app/gemini_client.py` | Thin Gemini SDK wrapper — only file that touches the AI SDK |
| `app/routes.py` | FastAPI router — pure HTTP, delegates everything to the modules above |

---

## Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | None | Liveness probe (Docker & Gateway) |
| `GET` | `/docs` | None | Swagger UI |
| `GET` | `/api/chatbot/questions` | None | Suggested starter questions |
| `POST` | `/api/chatbot/message` | Optional JWT | Send a message, get Aria's reply |
| `DELETE` | `/api/chatbot/session/{id}` | None | Clear conversation history |
| `GET` | `/api/chatbot/stats` | None | Active session count (diagnostic) |

---

## Running locally (without Docker)

```bash
cd ChatbotService.API
pip install -r requirements.txt

# set env vars
$env:GEMINI_API_KEY      = "your-key"
$env:FLIGHT_SERVICE_URL  = "http://localhost:5090"
$env:BOOKING_SERVICE_URL = "http://localhost:5122"

uvicorn main:app --reload --port 8000
# → Swagger UI: http://localhost:8000/docs
```

## Running via Docker Compose

```bash
# From the solution root
docker-compose up chatbot-service
# Service available at http://localhost:8100
# Through gateway  at http://localhost:5049/chatbot/api/chatbot/message
```

---

## Interview talking points

1. **"Why Python for this one service?"**  
   The Gemini SDK, async HTTP with `httpx`, and Pydantic validation are all first-class in Python. The other services are C# because EF Core + RabbitMQ + Ocelot are better there.

2. **"How does the Angular frontend talk to it?"**  
   Via the Ocelot API Gateway — same entry point as all other services. The frontend has zero awareness that this is Python.

3. **"How do you keep the Gemini API key secure?"**  
   It's an environment variable injected at container startup. It never reaches the browser.

4. **"How does it know the user's bookings?"**  
   The Angular app forwards its JWT; the chatbot passes it to the Booking microservice; the booking service validates it and returns only that passenger's data.

5. **"What if this service goes down?"**  
   Fault isolation — the other five microservices continue running normally. The chat UI just gets an error. Bookings and payments are unaffected.

6. **"Can it scale independently?"**  
   Yes. In Docker Compose you'd set `replicas: N`. In Kubernetes you'd use a separate HPA. Stateless-ness is already enforced (session store can move to Redis).
