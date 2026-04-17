# Proyecto: Capacitaciones

## 1. Visión general

Sistema de registro y gestión de capacitaciones. El equipo está compuesto por un **PM orquestador** (Claude) y cuatro agentes especializados que se activan bajo demanda según la necesidad de cada tarea. Este documento es la fuente de verdad para la continuidad del proyecto: cualquier retomada de trabajo debe iniciar leyendo este archivo.

## 2. Stack tecnológico

| Capa            | Tecnología                                         |
| --------------- | -------------------------------------------------- |
| Frontend        | React + Vite, arquitectura basada en componentes   |
| Estilos         | CSS/SCSS del directorio `./style/` (design tokens) |
| Servidor web    | Nginx (sirve el build del front)                   |
| Backend         | .NET Core 8, arquitectura hexagonal                |
| Persistencia    | SQL Server                                         |
| Infraestructura | Docker + Docker Compose                            |
| Calidad/Sec     | SonarQube + OWASP ZAP (contenedores)               |

## 3. Organización del equipo

### 3.1 PM / Orquestador (Claude)

- Único punto de coordinación. Recibe el requerimiento, lo descompone y despacha las tareas al agente correspondiente.
- Mantiene actualizado este documento (`instrucciones.md`) cuando se introducen cambios estructurales.
- Valida la integración entre capas antes de dar un entregable por cerrado.
- No escribe código de producción; delega en los agentes.

### 3.2 Agente Frontend

**Misión:** construir la SPA en React siguiendo los requerimientos funcionales.

**Directrices:**
- Arquitectura por componentes reutilizables. Separar en `components/` (presentacionales), `pages/` (vistas), `hooks/`, `services/` (cliente HTTP), `layouts/`.
- **Obligatorio** consumir los estilos del directorio `./style/`:
  - `variables.css` y `design-tokens.*` definen la paleta, espaciados y tipografía.
  - `components.css`, `utilities.css` y `main.css` entregan clases base y utilitarias.
  - `example.html` es la referencia visual a respetar.
- No introducir librerías de estilo alternativas (Bootstrap, MUI, Tailwind) salvo autorización expresa del PM.
- Consumo del backend mediante un cliente HTTP centralizado; nada de `fetch`/axios dispersos en componentes.
- El build (`npm run build`) se publica en el volumen compartido `/html` del contenedor Nginx.
- **Página inicial:** layout con `Sidebar` (navegación) + `Body` (área de trabajo). El contenido del body se define a medida que se soliciten módulos.

### 3.3 Agente Backend

**Misión:** implementar la API en .NET Core 8 bajo arquitectura hexagonal.

**Directrices:**
- Estructura de proyectos:
  - `Capacitaciones.Domain` — entidades, value objects, puertos (interfaces).
  - `Capacitaciones.Application` — casos de uso, DTOs, orquestación.
  - `Capacitaciones.Infrastructure` — adaptadores: persistencia EF Core (SQL Server), servicios externos.
  - `Capacitaciones.Api` — adaptador HTTP (controladores / endpoints minimal API), DI, configuración.
- Dependencias apuntan hacia el dominio (regla de dependencias hexagonal).
- Persistencia: SQL Server vía EF Core. Migraciones versionadas en `Infrastructure/Persistence/Migrations`.
- Exponer documentación con Swagger/OpenAPI.
- Configuración por `appsettings.{env}.json` + variables de entorno; nunca credenciales en el repo.
- Health check en `/health`.

### 3.4 Agente Infraestructura

**Misión:** proveer todo el entorno de ejecución mediante Docker.

**Red y direccionamiento:**
- Red Docker nombrada: `capacitaciones-net`
- Subred: `192.168.56.0/24`
- Asignación de IPs a partir de `.10`:

| Servicio        | IP              | Puerto expuesto |
| --------------- | --------------- | --------------- |
| nginx (front)   | 192.168.56.10   | 80              |
| backend (.NET)  | 192.168.56.11   | 8080            |
| sqlserver       | 192.168.56.12   | 1433            |
| sonarqube       | 192.168.56.13   | 9000            |
| sonar-db (pg)   | 192.168.56.14   | 5432            |
| owasp-zap       | 192.168.56.15   | 8090            |

**Entregables:**
- `docker-compose.yml` raíz que orqueste todos los servicios en `capacitaciones-net`.
- Carpeta `infra/nginx/`:
  - `nginx.conf` y `default.conf` — configuración del sitio, SPA fallback (`try_files $uri /index.html`), proxy `/api` → backend.
  - Volumen `/html` montado donde el agente Frontend coloca el build.
- Carpeta `infra/backend/Dockerfile` — multistage build .NET 8.
- Carpeta `infra/sqlserver/` — imagen oficial `mcr.microsoft.com/mssql/server:2022-latest`, volumen persistente para datos.
- Carpeta `infra/sonarqube/` — compose parcial con PostgreSQL para Sonar.
- Carpeta `infra/zap/` — configuración de escaneo OWASP ZAP.
- Variables sensibles en `.env` (no versionado); plantilla `.env.example` sí versionada.

### 3.5 Agente Security

**Misión:** asegurar calidad de código y postura de seguridad.

**Herramientas:**
- **SonarQube** (contenedor) — análisis estático de Frontend y Backend.
  - Proyecto `capacitaciones-frontend` y `capacitaciones-backend` con sus respectivos `sonar-project.properties`.
  - Quality Gate bloqueante antes de integrar.
- **OWASP ZAP** (contenedor) — escaneo dinámico contra la API y el front desplegados.
  - Baseline scan automatizado; reporte en `security/reports/`.
- Validación de dependencias:
  - Frontend: `npm audit`.
  - Backend: `dotnet list package --vulnerable`.

**Criterios de aceptación:**
- Sin vulnerabilidades `High`/`Critical` abiertas sin justificación documentada.
- Cobertura mínima y duplicación máxima según Quality Gate definido en Sonar.

## 4. Flujo de trabajo orquestado

1. El usuario entrega un requerimiento al PM.
2. PM descompone el requerimiento e identifica qué agentes intervienen.
3. Se activan los agentes (en paralelo cuando no hay dependencia).
4. Al completarse la implementación, el agente Security corre Sonar + ZAP.
5. PM valida integración end-to-end y reporta al usuario.
6. Todo lo relevante para la continuidad se registra en este documento.

## 5. Estructura de directorios objetivo

```
Capacitaciones/
├── instrucciones.md
├── docker-compose.yml
├── .env.example
├── style/                       # design system (ya provisto)
├── frontend/                    # React + Vite
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   ├── layouts/
│   │   ├── hooks/
│   │   └── services/
│   └── Dockerfile               # build stage
├── backend/
│   ├── src/
│   │   ├── Capacitaciones.Domain/
│   │   ├── Capacitaciones.Application/
│   │   ├── Capacitaciones.Infrastructure/
│   │   └── Capacitaciones.Api/
│   └── tests/
├── infra/
│   ├── nginx/
│   │   ├── nginx.conf
│   │   └── default.conf
│   ├── sqlserver/
│   ├── sonarqube/
│   └── zap/
└── security/
    ├── sonar-project.properties
    └── reports/
```

## 6. Convenciones transversales

- Idioma de código/identificadores: inglés. Comentarios y documentación de usuario: español.
- Commits siguen Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`).
- Branching: `main` (estable) + feature branches (`feat/<tema>`).
- Versionado semántico a partir de la primera release.
- Ningún secreto en el repositorio.

## 7. Alcance funcional — Módulo Capacitaciones (v1)

### 7.1 Entidades de dominio

**Capacitacion**
- `Id` (GUID)
- `Codigo` — formato `CAP-PC-REG-##` (generado por contador, ver 7.4).
- `Tema` (string, requerido)
- `CapacitadorId` → Capacitador (al menos nombre; se amplía si se requiere catálogo aparte)
- `ModalidadId` → Catálogo Modalidad
- `TipoActividadId` → Catálogo TipoActividad
- `FechaHoraInicio` (datetime)
- `DuracionMinutos` (int) — *(unidad por confirmar)*
- `Descripcion` (string, nullable) — la carga el capacitador vía link compartido
- `FirmaCapacitador` (blob/base64, nullable) — dibujada o cargada
- `Finalizada` (bool, derivado o manual — por confirmar)

**Catálogos administrables (todos con CRUD + import/export XLSX):**
- `Modalidad` — seed: *Presencial, Virtual, Híbrida*
- `TipoActividad` — seed: *Charla, Workshop, Capacitación, Curso, Taller, Seminario*
- `Area` — sin seed (lo definirá el usuario)

**Asistente (inscripción pública)**
- `Id` (GUID)
- `CapacitacionId` (FK)
- `Nombres`, `Apellidos`
- `Identificacion` (cédula/pasaporte)
- `AreaId` → Catálogo Área
- `EmailUsuario` (solo la parte local; el backend almacena `{usuario}@dos.com.ec`)
- `Firma` (blob/base64 — dibujada o cargada)
- `FechaInscripcion` (datetime)

### 7.2 Vistas / pantallas

1. **Dashboard de Capacitaciones** — grid de cards (largos, alto corto):
   - Título: tema de la capacitación.
   - Línea 2: icono persona + nombre del capacitador.
   - Línea 3: fecha, hora inicio, duración, modalidad.
   - Línea 4: N° de asistentes inscritos + estado (finalizada / pendiente).
   - Lado derecho del card: acciones
     - **Link capacitador** (copia URL firmada para que el capacitador cargue descripción + firma).
     - **Asistentes** (abre vista de listado de inscritos).
     - **Enlace de inscripción** (copia URL pública para inscripción de asistentes).
   - Botón "Nueva capacitación" → abre modal con formulario.

2. **CRUD por catálogo** (Modalidad, TipoActividad, Área):
   - Tabla + modal de creación/edición.
   - Botón **Descargar plantilla XLSX** (formato vacío con encabezados).
   - Botón **Cargar plantilla XLSX** (upload → import masivo con validación).

3. **Configuración de numeración**:
   - Pantalla dedicada para establecer el **número inicial** del contador de `CAP-PC-REG-##`.
   - A partir de ese valor, toda nueva capacitación auto-incrementa.
   - Mostrar el siguiente número a asignar.

4. **Página del capacitador** (acceso por link firmado, sin login):
   - Muestra datos de la capacitación (solo lectura).
   - Formulario: descripción + firma (dibujo en canvas o upload).

5. **Página pública de inscripción** (acceso por link firmado, sin login):
   - Muestra datos de la capacitación.
   - Formulario: nombres, apellidos, identificación, área (select del catálogo), usuario de correo (campo con sufijo fijo `@dos.com.ec` visualmente anclado y no editable), firma.
   - Botón "Inscribirme".

6. **Listado de asistentes por capacitación**:
   - Tabla simple; se accede desde el card.

### 7.3 Reglas UX transversales

- **Modales:** ningún modal cierra al hacer click fuera. Cierre válido solo por:
  - Tecla **ESC**.
  - Botón **X** (esquina).
  - Botón **Cerrar** / **Cancelar**.
  - Cumplimiento exitoso del formulario (submit correcto).
- **Email en inscripción:** el input visible capta solo el usuario; el dominio `@dos.com.ec` se concatena en backend. El UI muestra el sufijo como addon no editable.
- **Firma:** componente reutilizable `SignaturePad` con dos modos: dibujar (canvas) o cargar archivo (PNG/JPG).

### 7.4 Generación de código `CAP-PC-REG-##`

- Se mantiene un contador persistente (`ConfiguracionNumeracion` o similar) con el próximo número a usar.
- La pantalla de configuración permite fijar ese contador (solo si no rompe unicidad — validar que sea mayor al máximo actual, o permitir reset con confirmación).
- Al crear capacitación, el backend toma y avanza el contador en una transacción para evitar colisiones.
- *A confirmar:* padding de `##` (¿dos dígitos fijos o crece dinámicamente?).

### 7.5 Import/Export XLSX de catálogos

- Librería backend: `ClosedXML` o `EPPlus` (a definir por agente Backend).
- Endpoint `GET /catalogos/{tipo}/plantilla` → descarga plantilla vacía.
- Endpoint `POST /catalogos/{tipo}/importar` → recibe archivo, valida fila por fila, reporta errores.

## 8. Estado actual

- [x] Design system en `./style/` listo para consumo.
- [x] `instrucciones.md` creado y alcance v1 registrado.
- [ ] Scaffolding Backend (.NET 8 hexagonal) — Domain/Application/Infrastructure/Api.
- [ ] Scaffolding Frontend (React + Vite + layout Sidebar/Body).
- [ ] `docker-compose.yml` + red `capacitaciones-net` + nginx confs.
- [ ] Módulo Catálogos (Modalidad, TipoActividad, Área) — CRUD + XLSX.
- [ ] Pantalla Configuración de numeración.
- [ ] Módulo Capacitaciones — CRUD + grid de cards.
- [ ] Página capacitador (link firmado).
- [ ] Página pública de inscripción + componente `SignaturePad`.
- [ ] Listado de asistentes por capacitación.
- [ ] Integración SonarQube + OWASP ZAP.
- [ ] Inicialización de repositorio Git y primer push.

## 9. Plan de fases

| Fase | Objetivo                                                     | Agentes involucrados     |
| ---- | ------------------------------------------------------------ | ------------------------ |
| 0    | Bootstrap: scaffolds front/back + compose + nginx            | Backend, Frontend, Infra |
| 1    | Catálogos (Modalidad, TipoActividad, Área) + import/export   | Backend, Frontend        |
| 2    | Configuración de numeración                                  | Backend, Frontend        |
| 3    | CRUD Capacitaciones + grid de cards + modales                | Backend, Frontend        |
| 4    | Link capacitador (descripción + firma)                       | Backend, Frontend        |
| 5    | Página pública de inscripción + firma + asistentes           | Backend, Frontend        |
| 6    | Pasada de calidad y seguridad (Sonar + ZAP)                  | Security                 |
| 7    | Versionado Git + primer push                                 | PM                       |

## 10. Decisiones pendientes antes de Fase 1

1. **Unidad de duración:** ¿minutos u horas decimales? (sugerencia PM: minutos).
2. **Padding del código:** `CAP-PC-REG-01` (2 dígitos fijos) vs `CAP-PC-REG-1` (crece). Sugerencia PM: 3 dígitos `001` para crecer sin romper el formato.
3. **Autenticación:** ¿necesitamos login para el panel admin en esta v1 o queda abierto dentro de la red interna? Los links de capacitador y de inscripción sí serán tokens firmados sin login.
4. **Capacitador:** ¿texto libre por ahora o se convierte en catálogo administrable (personas)?
5. **"Finalizada":** ¿se marca manualmente o se infiere al pasar `FechaHoraInicio + Duracion`?
6. **SQL Server edición:** Developer (gratis) para dev local — OK por defecto.
