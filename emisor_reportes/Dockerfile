FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PORT=5000 \
    OUTPUT_DIR=/output \
    ASSETS_DIR=/app/assets

WORKDIR /app

# Fuentes del sistema (Noto Sans es buen fallback; la tipografía de marca
# se embede más adelante si se requiere — reportlab usa las TTF que le indiquemos).
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        fontconfig \
        fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY src/ ./src/
COPY assets/ ./assets/

EXPOSE 5000

CMD ["gunicorn", "-w", "2", "-b", "0.0.0.0:5000", "--timeout", "120", "src.app:app"]
