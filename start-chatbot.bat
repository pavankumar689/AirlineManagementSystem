@echo off
echo Starting Veloskyra Chatbot Service (Aria)...
echo Make sure you have python installed and run "pip install -r ChatbotService.API\requirements.txt" first.
echo.

set GEMINI_API_KEY=AIzaSyCsISj7JsQ5AAIcZHCCXBi4H7gt96tP83o
set FLIGHT_SERVICE_URL=http://localhost:5090
set BOOKING_SERVICE_URL=http://localhost:5122
set AUTH_SERVICE_URL=http://localhost:5228

cd ChatbotService.API
uvicorn main:app --host 0.0.0.0 --port 8000 --reload
