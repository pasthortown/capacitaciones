# syntax=docker/dockerfile:1.6
# Multistage build para la API .NET 8 (proyecto Capacitaciones.Api).
# Build context esperado: raiz del repo (ver docker-compose.yml).

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar todo el backend para que restore pueda resolver referencias entre proyectos.
COPY backend/ ./

# Restore + publish del proyecto API.
RUN dotnet restore src/Capacitaciones.Api/Capacitaciones.Api.csproj \
 && dotnet publish src/Capacitaciones.Api/Capacitaciones.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Usuario no-root (la imagen aspnet:8.0 ya incluye el uid 1654 "app").
USER app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Capacitaciones.Api.dll"]
