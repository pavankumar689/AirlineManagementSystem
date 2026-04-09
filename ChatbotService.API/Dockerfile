FROM public.ecr.aws/docker/library/python:3.12-slim AS builder

WORKDIR /install

COPY requirements.txt .
RUN pip install --no-cache-dir --prefix=/runtime -r requirements.txt


FROM public.ecr.aws/docker/library/python:3.12-slim

LABEL maintainer="Veloskyra Engineering"
LABEL description="Aria — AI Chatbot Microservice (Python / FastAPI)"
LABEL version="2.0.0"

WORKDIR /app

# Copy compiled packages from builder (keeps this image small — no pip in prod)
COPY --from=builder /runtime /usr/local

# Copy application source
COPY main.py .

# Expose the Uvicorn port
EXPOSE 8000

# Healthcheck — Docker will mark the container unhealthy if Aria stops responding
HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=5 \
  CMD python -c "import urllib.request; urllib.request.urlopen('http://localhost:8000/health')"

# Run with 2 Uvicorn workers (bump to CPU count in production)
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "2"]
