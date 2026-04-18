# syntax=docker/dockerfile:1.6
# ============================================================
#  emisor_documentos — Node 20 + Puppeteer + Chromium
# ============================================================
FROM node:20-slim

ENV DEBIAN_FRONTEND=noninteractive \
    PUPPETEER_SKIP_DOWNLOAD=false \
    NODE_ENV=production \
    TZ=America/Guayaquil

# Dependencias del sistema requeridas por Chromium (Puppeteer docs).
RUN apt-get update && apt-get install -y --no-install-recommends \
      ca-certificates \
      fonts-liberation \
      libasound2 \
      libatk-bridge2.0-0 \
      libatk1.0-0 \
      libc6 \
      libcairo2 \
      libcups2 \
      libdbus-1-3 \
      libexpat1 \
      libfontconfig1 \
      libgbm1 \
      libgcc-s1 \
      libglib2.0-0 \
      libgtk-3-0 \
      libnspr4 \
      libnss3 \
      libpango-1.0-0 \
      libpangocairo-1.0-0 \
      libstdc++6 \
      libx11-6 \
      libx11-xcb1 \
      libxcb1 \
      libxcomposite1 \
      libxcursor1 \
      libxdamage1 \
      libxext6 \
      libxfixes3 \
      libxi6 \
      libxrandr2 \
      libxrender1 \
      libxss1 \
      libxtst6 \
      lsb-release \
      wget \
      xdg-utils \
      tzdata \
    && ln -snf /usr/share/zoneinfo/$TZ /etc/localtime \
    && echo $TZ > /etc/timezone \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Instalar dependencias primero (cache layer).
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund \
    && npx --yes puppeteer browsers install chrome \
    && chown -R node:node /app /home/node /root/.cache 2>/dev/null || true

# Mover el cache de Chromium a una ruta accesible por el usuario `node`.
ENV PUPPETEER_CACHE_DIR=/home/node/.cache/puppeteer
RUN mkdir -p /home/node/.cache \
    && if [ -d /root/.cache/puppeteer ]; then cp -r /root/.cache/puppeteer /home/node/.cache/puppeteer; fi \
    && chown -R node:node /home/node/.cache

# Copiar código y assets.
COPY src ./src
COPY templates ./templates

# Asegurar escritura para el usuario no-root.
RUN mkdir -p /output && chown -R node:node /output /app

USER node

ENV PORT=3000 \
    TEMPLATES_DIR=/app/templates \
    OUTPUT_DIR=/output

EXPOSE 3000

CMD ["node", "src/server.js"]
