FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

# `pymssql` necesita libssl/libcrypto y FreeTDS — la wheel de pypi
# trae todo precompilado para linux, así que no requiere apt extra.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py .

CMD ["python", "-u", "app.py"]
