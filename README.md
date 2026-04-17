# capacitaciones

Sistema de registro y gestión de capacitaciones.

La definición completa del proyecto (equipo, arquitectura, stack, red Docker, alcance funcional y plan por fases) está en [`instrucciones.md`](./instrucciones.md).

## Stack

- **Frontend:** React + Vite, servido por Nginx.
- **Backend:** .NET Core 8, arquitectura hexagonal.
- **Persistencia:** SQL Server.
- **Infraestructura:** Docker Compose, red `capacitaciones-net` (`192.168.56.0/24`).
- **Calidad / Seguridad:** SonarQube + OWASP ZAP.

## Estructura

```
.
├── instrucciones.md     # fuente de verdad del proyecto
├── HU/                  # historias de usuario
├── style/               # design system (CSS + tokens)
├── frontend/            # (pendiente) SPA React
├── backend/             # (pendiente) API .NET 8
└── infra/               # (pendiente) compose + nginx + sqlserver + sonar + zap
```
