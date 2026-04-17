# syntax=docker/dockerfile:1.6
# ============================================================================
# Frontend build image (Capacitaciones SPA)
# ----------------------------------------------------------------------------
# Uso principal (flujo docker-compose del proyecto):
#   El compose monta ./frontend/dist en el contenedor nginx. Se espera que el
#   artefacto de build ya exista (producido por `npm run build` en la maquina
#   del dev o en CI). Este Dockerfile cubre el flujo alternativo build-as-image
#   para pipelines CI/CD.
#
# Ejemplo (build-as-image):
#   docker build -t capacitaciones-frontend:local ./frontend
#   docker run --rm -v "$(pwd)/frontend/dist:/out" capacitaciones-frontend:local \
#     sh -c "cp -r /app/dist/. /out"
# ============================================================================

FROM node:20-alpine AS build
WORKDIR /app

# El design system vive fuera de /frontend, pero src/styles/index.css lo
# importa por ruta relativa (../../../style/*.css). Para que el build en
# Docker funcione, se debe construir desde la raiz del proyecto con el
# contexto adecuado, o copiar /style al contenedor:
#
#   docker build -f frontend/Dockerfile -t capacitaciones-frontend .
#
# Por defecto asumimos contexto = ./frontend (no incluye /style). En ese caso
# el consumidor debe hacer `npm run build` localmente y montar ./frontend/dist.

COPY package*.json ./
RUN npm ci --no-audit --no-fund

COPY . .

RUN npm run build

# Etapa final: imagen liviana que solo sirve para exportar /app/dist
FROM alpine:3.20 AS export
WORKDIR /app
COPY --from=build /app/dist ./dist
CMD ["sh", "-c", "echo 'Artefacto listo en /app/dist'; ls -la /app/dist"]
