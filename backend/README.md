# Backend — Capacitaciones

API .NET 8 bajo arquitectura hexagonal.

## Estructura

```
backend/
├── Capacitaciones.sln
├── src/
│   ├── Capacitaciones.Domain/         # entidades, puertos, VOs
│   ├── Capacitaciones.Application/    # casos de uso, DTOs
│   ├── Capacitaciones.Infrastructure/ # adaptadores: EF Core / SQL Server
│   └── Capacitaciones.Api/            # HTTP: controladores, Swagger, DI
└── tests/
    └── Capacitaciones.Tests/          # xUnit + Mvc.Testing
```

**Regla de dependencias hexagonal**

```
Api ──► Application ──► Domain
 │            ▲
 └─► Infrastructure ┘
```

## Requisitos

- .NET SDK 8.0+

## Comandos

Desde `./backend/`:

```bash
# Restaurar paquetes
dotnet restore

# Compilar toda la solución
dotnet build

# Ejecutar la API (perfil http, puerto 8080)
dotnet run --project src/Capacitaciones.Api

# Tests
dotnet test
```

La API respeta la variable `ASPNETCORE_URLS`; por defecto (perfil dev) escucha en `http://localhost:8080`.

## Endpoints base

- `GET /health` — health check.
- `GET /swagger` — documentación OpenAPI (solo Development).

## Configuración

- `appsettings.json` — valores por defecto (vacíos para secretos).
- `appsettings.Development.json` — overrides de logging.
- La cadena de conexión `ConnectionStrings:Default` y la sección `Jwt` se inyectan por variables de entorno o `.env` en despliegue Docker.
