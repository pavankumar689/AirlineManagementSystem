"""
Veloskyra Chatbot Microservice — Python / FastAPI
Calls the Gemini API server-side (no API key in browser),
fetches live flight & booking data from other services,
and manages per-session conversation history.
"""

import os
import uuid
import logging
from datetime import datetime, timezone
from typing import Optional

import httpx
from fastapi import FastAPI, HTTPException, Header
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import google.generativeai as genai

# ──────────────────────────────────────────────
#  Configuration
# ──────────────────────────────────────────────
GEMINI_API_KEY   = os.getenv("GEMINI_API_KEY", "AIzaSyCsISj7JsQ5AAIcZHCCXBi4H7gt96tP83o")
FLIGHT_SERVICE   = os.getenv("FLIGHT_SERVICE_URL", "http://flight-service:8080")
BOOKING_SERVICE  = os.getenv("BOOKING_SERVICE_URL", "http://booking-service:8080")
AUTH_SERVICE     = os.getenv("AUTH_SERVICE_URL", "http://auth-service:8080")

genai.configure(api_key=GEMINI_API_KEY)

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("aria")

app = FastAPI(
    title="Veloskyra Chatbot Service (Aria)",
    description="AI chatbot microservice powered by Gemini — written in Python 🐍",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# ──────────────────────────────────────────────
#  In-memory session store  {session_id: [history]}
#  (Good enough for a demo; swap for Redis in prod)
# ──────────────────────────────────────────────
sessions: dict[str, list[dict]] = {}

MAX_HISTORY = 20   # keep last N turns per session


# ──────────────────────────────────────────────
#  Pydantic models
# ──────────────────────────────────────────────
class ChatRequest(BaseModel):
    message: str
    session_id: Optional[str] = None   # client reuses the same id across turns


class ChatResponse(BaseModel):
    reply: str
    session_id: str


class SuggestionsResponse(BaseModel):
    questions: list[str]


# ──────────────────────────────────────────────
#  Helpers
# ──────────────────────────────────────────────
def _now_label() -> str:
    """Human-readable current datetime sent to the LLM."""
    now = datetime.now(timezone.utc)
    return now.strftime("%A, %B %d, %Y %I:%M %p")   # e.g.  Tuesday, April 07, 2026 03:30 PM


def _parse_dt(iso_str: str) -> datetime:
    """
    Parse an ISO datetime string and always return a timezone-aware (UTC) datetime.
    Handles both:
      - "2026-04-28T22:00:00"        (naive  — DB stores UTC without offset)
      - "2026-04-28T22:00:00Z"       (aware, Z suffix)
      - "2026-04-28T22:00:00+00:00"  (aware, explicit offset)
    """
    dt = datetime.fromisoformat(iso_str.replace("Z", "+00:00"))
    if dt.tzinfo is None:
        # Naive string — the DB stores UTC, so attach UTC explicitly
        dt = dt.replace(tzinfo=timezone.utc)
    return dt


async def _fetch_flight_context() -> str:
    """Pull upcoming schedules from the Flight service."""
    try:
        async with httpx.AsyncClient(timeout=8) as client:
            r = await client.get(f"{FLIGHT_SERVICE}/api/schedule")
            r.raise_for_status()
            schedules = r.json()

        now = datetime.now(timezone.utc)  # must be offset-aware to compare with API datetimes
        upcoming = [
            s for s in schedules
            if _parse_dt(s.get("departureTime", "2000-01-01T00:00:00")) >= now
        ]
        upcoming.sort(key=lambda x: _parse_dt(x.get("departureTime", "2000-01-01T00:00:00")))
        upcoming = upcoming[:80]

        if not upcoming:
            return "AVAILABLE UPCOMING FLIGHTS: None at the moment."

        lines = []
        for s in upcoming:
            fn  = s.get("flight", {}).get("flightNumber", "?")
            org = s.get("flight", {}).get("originAirport", {}).get("code", "?")
            dst = s.get("flight", {}).get("destinationAirport", {}).get("code", "?")
            dep = _parse_dt(s["departureTime"]).strftime("%B %d, %Y")
            eco = s.get("economyPrice", "?")
            biz = s.get("businessPrice", "?")
            status = s.get("status", "?")
            lines.append(
                f"• {fn}: {org} → {dst} | {dep} | Eco:₹{eco} | Biz:₹{biz} | {status}"
            )

        return "AVAILABLE UPCOMING FLIGHTS (up to 80):\n" + "\n".join(lines)
    except Exception as e:
        log.warning("Could not fetch flight schedules: %s", e)
        return "AVAILABLE UPCOMING FLIGHTS: (unavailable right now)"


async def _fetch_booking_context(auth_header: Optional[str]) -> str:
    """Pull the logged-in passenger's bookings from the Booking service."""
    if not auth_header:
        return ""
    try:
        async with httpx.AsyncClient(timeout=8) as client:
            r = await client.get(
                f"{BOOKING_SERVICE}/api/booking/my-bookings",
                headers={"Authorization": auth_header},
            )
            r.raise_for_status()
            bookings = r.json()

        if not bookings:
            return "PASSENGER BOOKINGS: None found."

        lines = [
            f"• PNR:{b.get('pnr')} | {b.get('flightNumber')} | "
            f"{b.get('origin')}→{b.get('destination')} | "
            f"Seat:{b.get('seatNumber')}({b.get('class')}) | "
            f"{b.get('bookingStatus')} | ₹{b.get('totalAmount')}"
            for b in bookings
        ]
        return "PASSENGER BOOKINGS:\n" + "\n".join(lines)
    except Exception as e:
        log.warning("Could not fetch bookings: %s", e)
        return ""


def _build_system_prompt(
    passenger_name: str,
    logged_in: bool,
    flight_ctx: str,
    booking_ctx: str,
) -> str:
    ctx_parts = [flight_ctx]
    if booking_ctx:
        ctx_parts.append(booking_ctx)
    context = "\n\n".join(ctx_parts) or "No live data at the moment."

    return f"""You are "Aria", a warm, helpful AI assistant for Veloskyra Airlines.
Be friendly, concise, and use emojis (✈️🎫🧳💺) naturally. Never sound robotic. Vary your responses.

PASSENGER: {passenger_name} | Logged in: {logged_in}
CURRENT DATE & TIME: {_now_label()}

LIVE DATABASE DATA (Upcoming Flights & Bookings):
{context}

POLICIES:
- Cancellation: 10% fee, refund in 5-7 business days, cancel from My Bookings page
- Baggage: Economy=15kg+7kg cabin, Business=30kg+10kg cabin, excess=₹500/kg
- Check-in: Web opens 24hrs before, counter closes 45min before departure
- Seats: Window (A&F) = 15% extra, no changes after booking confirmed
- Reward Points: 1pt=₹1, max 60% of booking value
- Payment: UPI, Card, Net Banking via Razorpay
- Alerts: Subscribe via Alerts page, get email for flight status changes
- Routes: Delhi/Mumbai to Dubai/London/NYC/Singapore/Tokyo/Sydney/Paris/SF + domestic India
- Support email: support@veloskyra.com

RULES:
- Answer booking questions using the LIVE DATA above
- If user asks about bookings but is not logged in, ask them to log in
- Keep answers concise (3-5 sentences), use bullets for lists
- End with an offer to help with something else"""


# ──────────────────────────────────────────────
#  Routes
# ──────────────────────────────────────────────
@app.get("/health")
def health():
    return {"status": "ok", "service": "ChatbotService (Aria)", "lang": "Python"}


@app.get("/api/chatbot/questions", response_model=SuggestionsResponse)
def get_suggestions(logged_in: bool = False):
    if logged_in:
        return SuggestionsResponse(questions=[
            "What are my upcoming bookings?",
            "Can I cancel my booking?",
            "What is the baggage allowance?",
            "How do I use my reward points?",
            "What's the check-in time?",
            "Show me flights from Delhi",
        ])
    return SuggestionsResponse(questions=[
        "What flights are available today?",
        "What is the baggage allowance?",
        "How does seat selection work?",
        "What payment methods do you accept?",
        "Can I get a refund if I cancel?",
        "Tell me about Business class",
    ])


@app.post("/api/chatbot/message", response_model=ChatResponse)
async def chat(
    req: ChatRequest,
    authorization: Optional[str] = Header(default=None),
    x_passenger_name: Optional[str] = Header(default=None),
):
    # Resolve / create session
    session_id = req.session_id or str(uuid.uuid4())
    if session_id not in sessions:
        sessions[session_id] = []

    history = sessions[session_id]

    # Determine identity
    logged_in = authorization is not None and authorization.startswith("Bearer ")
    passenger_name = x_passenger_name or ("Guest" if not logged_in else "Passenger")

    log.info("[Aria] session=%s | user=%s | msg=%s", session_id, passenger_name, req.message[:60])

    # Fetch live context in parallel
    flight_ctx, booking_ctx = await _fetch_flight_context(), await _fetch_booking_context(authorization)

    system_prompt = _build_system_prompt(passenger_name, logged_in, flight_ctx, booking_ctx)

    # Build Gemini contents from history + new message
    history.append({"role": "user", "parts": [{"text": req.message}]})

    try:
        model = genai.GenerativeModel(
            model_name="gemini-2.5-flash",
            system_instruction=system_prompt,
            generation_config={"temperature": 0.85, "max_output_tokens": 800},
        )
        response = model.generate_content(
            [{"role": m["role"], "parts": m["parts"]} for m in history]
        )
        reply_text = response.text

    except Exception as e:
        log.error("[Aria] Gemini error: %s", e)
        raise HTTPException(status_code=502, detail=f"AI service error: {str(e)}")

    # Save assistant turn & trim history
    history.append({"role": "model", "parts": [{"text": reply_text}]})
    if len(history) > MAX_HISTORY:
        sessions[session_id] = history[-MAX_HISTORY:]

    return ChatResponse(reply=reply_text, session_id=session_id)


@app.delete("/api/chatbot/session/{session_id}")
def clear_session(session_id: str):
    sessions.pop(session_id, None)
    return {"cleared": session_id}
