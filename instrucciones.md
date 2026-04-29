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

> ⚠️ **Build de producción — env vars obligatorias.** El SPA en producción se sirve bajo el prefijo `/capacitados/` (ver `/Docker/web/conf/capacitados.conf`) y el backend está expuesto en `/capacitados/api/...`. Cualquier rebuild que se vaya a desplegar al server debe ejecutarse con:
> ```bash
> VITE_BASE_PATH=/capacitados/ VITE_API_BASE=/capacitados/api npm run build
> ```
> Si se omite `VITE_BASE_PATH`, el `index.html` referencia assets en `/assets/...` y no se cargan; si se omite `VITE_API_BASE`, el bundle pega contra `/api/...` (que no existe en el ingress) y todas las llamadas — **incluido login** — fallan con 404.

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
| nginx (front)       | 192.168.56.10 | 80            |
| backend (.NET)      | 192.168.56.11 | 8080          |
| sqlserver (Express) | 192.168.56.12 | 1433          |
| sonarqube           | 192.168.56.13 | 9000          |
| sonar-db (pg)       | 192.168.56.14 | 5432          |
| owasp-zap           | 192.168.56.15 | 8090          |
| emisor_documentos   | 192.168.56.16 | 3000 (interno)|
| repository_httpd    | 192.168.56.17 | 80 (interno)  |

> `repository_httpd` sirve dos volúmenes distintos bajo un mismo httpd:
> - `/repository/` (alias raíz `/`) — material del módulo Repositorio.
> - `/imagen_capacitaciones/` (alias `/imagenes/`) — logos de capacitaciones (Fase 9).
> Ambos RW desde backend (`/repository`, `/imagen_capacitaciones`) y RO desde httpd.

**Entregables:**
- `docker-compose.yml` raíz que orqueste todos los servicios en `capacitaciones-net`.
- Carpeta `infra/nginx/`:
  - `nginx.conf` y `default.conf` — configuración del sitio, SPA fallback (`try_files $uri /index.html`), proxy `/api` → backend.
  - Volumen `/html` montado donde el agente Frontend coloca el build.
- Carpeta `infra/backend/Dockerfile` — multistage build .NET 8.
- Carpeta `infra/sqlserver/` — imagen oficial `mcr.microsoft.com/mssql/server:2022-latest` con `MSSQL_PID=Express` (última versión en edición **Express**), volumen persistente para datos.
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
│   ├── backend/
│   │   └── Dockerfile
│   ├── sqlserver/
│   ├── sonarqube/
│   └── zap/
├── emisor_documentos/           # servicio Node + Puppeteer (HTML→PDF)
│   ├── src/
│   ├── templates/               # certificado.html, fondo.png, certificado.png
│   ├── package.json
│   └── Dockerfile
├── output/                      # volumen compartido con PDFs generados (.gitignored)
├── certificados/
│   └── templates/               # assets de referencia y plantilla HTML versionada
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

### 6.1 Tipografías oficiales

Las fuentes canónicas del proyecto viven en `./Tipografía/` y son obligatorias **tanto en la aplicación web como en todo documento generado por el aplicativo** (certificados PDF, reportes, etc.). No se usan tipografías de sistema ni fuentes externas (Google Fonts, Adobe Fonts).

| Rol                          | Familia                | Ubicación origen                  |
| ---------------------------- | ---------------------- | --------------------------------- |
| Títulos (h1..h6)             | **Uni Sans**           | `./Tipografía/Uni-Sans-Títulos/`  |
| Texto corrido y UI           | **Montserrat**         | `./Tipografía/Montserrat-texto/`  |
| Display / auxiliar           | **Plus Jakarta Sans**  | `./Tipografía/PlusJakartaSans/`   |

**Distribución:**
- **Frontend:** las fuentes están copiadas en `./frontend/public/fonts/{Montserrat,UniSans,PlusJakartaSans}/` con nombres ASCII-safe, y declaradas como `@font-face` en `./frontend/src/styles/fonts.css`. Los tokens `--font-family-primary`, `--font-family-heading` y `--font-family-display` se sobrescriben en `./frontend/src/styles/overrides.css`.
- **`emisor_documentos`:** el servicio debe copiar las mismas fuentes a `./emisor_documentos/templates/fonts/` y embeberlas en el HTML del certificado via `@font-face` antes de que Puppeteer renderice. La fuente de verdad para ambas distribuciones es `./Tipografía/`.

## 7. Alcance funcional — Módulo Capacitaciones (v1)

### 7.1 Entidades de dominio

**Capacitacion**
- `Id` (GUID)
- `Codigo` — formato `CAP-PC-REG-###` con **3 dígitos fijos** (generado por contador, ver 7.4).
- `Tema` (string, requerido)
- `Capacitador` (**varchar(255), texto libre** — no es catálogo). Siempre figura como firmante en el certificado.
- `ModalidadId` → Catálogo Modalidad
- `TipoActividadId` → Catálogo TipoActividad
- `TipoCertificacion` (enum) — `Participacion` | `Aprobacion`. Se refleja en el certificado.
- `FechaHoraInicio` (datetime)
- `DuracionMinutos` (int) — múltiplo de **30 min**. El UI captura horas y minutos con steps de 30.
- `Descripcion` (string, nullable) — la carga el capacitador vía link firmado
- `FirmaCapacitador` (blob/base64, nullable) — dibujada o cargada
- `CargoCapacitador`, `EmpresaCapacitador` (varchar, nullable) — se capturan junto con la firma del capacitador y se imprimen bajo su firma en el certificado.
- `Estado` (derivado, no persistido — calculado en backend a partir del reloj):
  - `Inscripciones Abiertas` — desde su creación hasta `FechaHoraInicio`.
  - `Iniciada` — entre `FechaHoraInicio` y `FechaHoraInicio + DuracionMinutos`.
  - `Finalizada` — después de `FechaHoraInicio + DuracionMinutos`.

**Responsable** (firmantes adicionales del certificado — 0..N por capacitación)
- `Id` (GUID)
- `CapacitacionId` (FK)
- `Nombres` (varchar(255), requerido)
- `Cargo` (varchar(255), requerido)
- `Empresa` (varchar(255), requerido)
- `Firma` (blob/base64 — dibujada o cargada)
- `Orden` (int) — posición en que aparecen en el certificado.

> El **capacitador** siempre es el primer firmante (no se repite en la tabla `Responsable`). Los responsables adicionales se agregan desde la pantalla de edición de la capacitación.

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
   - Línea 4: N° de asistentes inscritos + estado (`Inscripciones Abiertas` / `Iniciada` / `Finalizada`).
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
   - Acción por fila: **descargar certificado** (solo disponible si la capacitación está `Finalizada`).

7. **Gestión de responsables** (dentro del formulario de capacitación):
   - Sub-sección en el modal de crear/editar capacitación.
   - Listado editable (agregar / eliminar) con `Nombres`, `Cargo`, `Empresa`, `Firma`.
   - Orden reorganizable (drag o flechas arriba/abajo).
   - Al menos 0 responsables adicionales (el capacitador siempre firma).

### 7.3 Reglas UX transversales

- **Modales:** ningún modal cierra al hacer click fuera. Cierre válido solo por:
  - Tecla **ESC**.
  - Botón **X** (esquina).
  - Botón **Cerrar** / **Cancelar**.
  - Cumplimiento exitoso del formulario (submit correcto).
- **Email en inscripción:** el input visible capta solo el usuario; el dominio `@dos.com.ec` se concatena en backend. El UI muestra el sufijo como addon no editable.
- **Firma:** componente reutilizable `SignaturePad` con dos modos: dibujar (canvas) o cargar archivo (PNG/JPG).
- **Duración:** se ingresa como `horas` + `minutos` con **step de 30 minutos** (minutos válidos: `0` o `30`). Persistencia en `DuracionMinutos`.

### 7.3.1 Autenticación y autorización

- **Panel administrativo (admin):** requiere **login**. Cubre gestión de catálogos, capacitaciones, configuración de numeración y listado de asistentes.
- **Link del capacitador:** URL con token firmado (JWT o similar) — sin login. Permite cargar descripción y firma de la capacitación asignada.
- **Link público de inscripción:** URL con token firmado — sin login. Permite a los asistentes registrarse.
- Los tokens deben ser específicos por recurso (capacitación) y revocables.

### 7.4 Generación de código `CAP-PC-REG-###`

- Formato: prefijo fijo `CAP-PC-REG-` + **3 dígitos con ceros a la izquierda** (`001`…`999`).
- Se mantiene un contador persistente (`ConfiguracionNumeracion`) con el próximo número a usar.
- La pantalla de configuración permite fijar ese contador (validar que sea mayor al máximo actual, o permitir reset con confirmación explícita).
- Al crear una capacitación, el backend toma y avanza el contador en una **transacción** para evitar colisiones.
- Al llegar a `999`, el sistema deberá avisar (política de rollover a definir en futura versión).

### 7.5 Import/Export XLSX de catálogos

- Librería backend: **ClosedXML** (licencia MIT, más simple que EPPlus).
- Endpoint `GET /catalogos/{tipo}/plantilla` → descarga plantilla vacía.
- Endpoint `POST /catalogos/{tipo}/importar` → recibe archivo, valida fila por fila, reporta errores.

### 7.6 Generación de certificados (servicio `emisor_documentos`)

**Disparo:** cuando la capacitación alcanza estado `Finalizada`, el backend expone un endpoint admin `POST /capacitaciones/{id}/certificados/generar` que genera un certificado por cada asistente inscrito. El endpoint también se puede invocar manualmente para regenerar.

**Servicio `emisor_documentos`** (nuevo contenedor Docker):
- **Stack:** Node.js 20 + Puppeteer (HTML → PDF fiable y maduro).
- **IP estática:** `192.168.56.16` en la red `capacitaciones-net`.
- **Puerto interno:** `3000` (no expuesto al host; solo accesible desde el backend por red interna).
- **API HTTP:**
  - `POST /emitir/certificado` — body JSON con el payload del certificado (ver más abajo). Responde `201 Created` con `{ ruta: "/output/<archivo>.pdf" }`.
  - `GET /health` — healthcheck.
- **Plantilla HTML:** archivo `templates/certificado.html` con tokens `{{asistente.nombres}}`, `{{capacitacion.tema}}`, `{{capacitacion.fecha}}`, `{{capacitacion.duracionHoras}}`, `{{capacitacion.tipoActividad}}`, `{{capacitacion.tipoCertificacion}}`, lista de firmantes (capacitador + responsables). **Tipografías obligatorias según sección 6.1** — embebidas vía `@font-face` apuntando a `./templates/fonts/`.
- **Assets estáticos:** `templates/fondo.png` (logo DOS + borde) se inserta como background-image del `<body>`. Las firmas se incrustan como `<img src="data:image/png;base64,...">`.
- **Volumen de salida:** `/output` dentro del contenedor, mapeado a `./output/` en el host y también montado en otros servicios que consuman los PDFs.

**Payload `POST /emitir/certificado`:**
```json
{
  "capacitacion": {
    "codigo": "CAP-PC-REG-001",
    "tema": "...",
    "tipoActividad": "Curso",
    "tipoCertificacion": "Aprobacion",
    "fechaInicio": "2026-05-10T09:00:00-05:00",
    "duracionHoras": 8.0
  },
  "asistente": {
    "nombres": "...",
    "apellidos": "...",
    "identificacion": "..."
  },
  "firmantes": [
    { "nombres": "Capacitador", "cargo": "...", "empresa": "...", "firmaBase64": "..." },
    { "nombres": "Responsable 1", "cargo": "...", "empresa": "...", "firmaBase64": "..." }
  ]
}
```

**Texto del certificado (según tipo de actividad):**
```
Certificado

[TipoCertificacion: Participación | Aprobación]

[Nombre completo del asistente]

Ha completado con éxito [la charla | el workshop | la capacitación | el curso | el taller | el seminario] sobre [Tema].

Dictado el [Fecha] con una duración de [Horas] horas.

[Firmas en fila: capacitador primero, luego responsables en el orden definido]
[Bajo cada firma: Nombre / Cargo / Empresa]
```

**Nombre de archivo PDF:** `{codigo}_{identificacion}.pdf` (ej. `CAP-PC-REG-001_1712345678.pdf`) dentro de `/output/`.

**Assets de referencia entregados por el usuario (en la raíz del repo):**
- `certificado.png` — render visual objetivo (calibración de posiciones y tipografías).
- `fondo.png` — imagen de fondo usada por la plantilla HTML (logo DOS + borde).
- `Formato certificado DOS.pdf` — especificación visual oficial.

Estos se reubicarán a `./emisor_documentos/templates/` al arrancar Fase 6.

### 7.7 Módulo Repositorio (material compartido)

**Propósito:** permitir al admin subir archivos (PDF, XLSX, imágenes, videos, etc.) hasta **100 MB**, gestionarlos (CRUD), y generar un **link público** para compartir la descarga sin login.

**Almacenamiento:**
- Volumen host `./repository/` compartido entre `capacitaciones-backend` (RW, en `/repository`) y `capacitaciones-repository-httpd` (RO, en `/usr/local/apache2/htdocs`). httpd cumple el rol de storage; el backend es quien lee/escribe.
- Nombre físico del archivo: `<Guid>.<ext>` (UUID generado al subir); el nombre original vive sólo en BD.
- Variable env: `REPOSITORIO_DIR=/repository`.

**Tabla `Recurso`:** `Id`, `NombreOriginal`, `NombreAlmacenado` (único), `Extension`, `ContentType`, `TamanoBytes`, `Descripcion`, `Activo`, `FechaCreacion`, `FechaActualizacion`.

**Endpoints:**
- Admin (policy `Admin`):
  - `POST /api/recursos` multipart — `archivo` + `nombre?` + `descripcion` → 201 `RecursoDetailDto`.
  - `GET /api/recursos?includeInactive=` — listado.
  - `GET /api/recursos/{id}` — detalle.
  - `PUT /api/recursos/{id}` — edita metadata (`nombreOriginal`, `descripcion`).
  - `DELETE /api/recursos/{id}` — soft delete + borrado físico.
  - `POST /api/recursos/{id}/link` → `{ url, recursoId, nombreOriginal, tamanoBytes, contentType }` con `url` relativa al endpoint público.
- Público (sin auth):
  - `GET /api/publico/recursos/{id}/descargar` — `FileStreamResult` con `Content-Disposition: attachment; filename="..."; filename*=UTF-8''...`. 404 si no existe/inactivo, 410 si el archivo físico está ausente.

**Política de extensiones (blacklist, case-insensitive):**
- Ejecutables: `exe, msi, com, scr, dll, bat, cmd, bin, apk, app, dmg, deb, rpm, jar, war`.
- Scripts: `sh, bash, zsh, ksh, ps1, psm1, psd1, vbs, vbe, wsf, wsh, js, jse, mjs, cjs, ts, py, pyc, pyw, rb, pl, php, phtml, reg, lnk, htaccess`.
- Centralizada en `ExtensionPolicy` (backend) y `BLOCKED_EXTENSIONS` (frontend, duplicada sólo para UX temprana).

**Límite de tamaño:** 100 MB (`Kestrel.Limits.MaxRequestBodySize` + `FormOptions.MultipartBodyLengthLimit` + `client_max_body_size 100M;` en nginx).

**UI (`/repositorio`, sidebar "Repositorio"):**
- Tabla con acciones por fila: copiar enlace (`navigator.clipboard` con fallback), editar metadata, eliminar.
- Modal de subida con validación client-side (extensión + tamaño) y modal de edición de metadata.

### 7.8 Puntaje mínimo + Logo de capacitación (Fase 9)

**Entidad `Capacitacion` extendida:**
- `PuntajeMinimo` (decimal(4,2), nullable 0–10) — **requerido solo** si `TipoCertificacion == Aprobacion`; `null` cuando es `Participacion`. Escala fija 0–10.
- `LogoPath` (varchar(500), nullable) — nombre físico dentro del volumen (ej. `<Guid>.png`).
- `LogoContentType` (varchar(100), nullable) — MIME original.

**Volumen `imagen_capacitaciones`:**
- Host: `./imagen_capacitaciones/` (.gitignored salvo `.gitkeep`).
- Backend (RW): `/imagen_capacitaciones`. Variable `IMAGEN_CAPACITACIONES_DIR=/imagen_capacitaciones`.
- `repository_httpd` (RO): `/usr/local/apache2/htdocs/imagenes` — servido como `/imagenes/<archivo>` (se agrega Alias en `httpd.conf`; la raíz `/` sigue sirviendo el módulo Repositorio).
- `emisor_documentos` (RO): `/imagen_capacitaciones` — lee el archivo local para embeberlo en el PDF sin salir por red.
- Nombre físico: `<Guid>.<ext>`. Whitelist case-insensitive: `png`, `jpg`, `jpeg`, `webp`, `svg`. Tamaño máximo: **2 MB**.

**Endpoints admin (policy `Admin`):**
- `POST /api/capacitaciones/{id}/logo` multipart `archivo` — `201` con `{ logoPath, logoContentType, logoUrl }`. Si ya había logo, se borra físicamente el anterior.
- `DELETE /api/capacitaciones/{id}/logo` — `204`. Borra archivo físico y limpia columnas.
- Al eliminar la capacitación (soft delete), se borra el archivo físico asociado.
- `CapacitacionDetailDto` y `CapacitacionListDto` exponen `logoUrl` (relativa, ej. `/imagenes/<Guid>.png`) para consumo directo por el front.

**UI (modal CRUD Capacitación):**
- Upload de logo con preview + botón "Eliminar".
- Campo `PuntajeMinimo` visible **solo** cuando `TipoCertificacion == Aprobacion` (step 0.1, min 0, max 10, requerido en ese caso).

### 7.9 Pase de lista (Fase 10)

**Entidad `Asistente` extendida:**
- `EstadoAsistencia` (enum: `null` | `Presente` | `Ausente`) — persiste la marcación.
- `FechaMarcacionAsistencia` (datetime, nullable).

**Link del capacitador — pase de lista:**
- Endpoint admin `POST /api/capacitaciones/{id}/link-pase-lista` emite JWT con `role=PaseLista`.
- Link reusable: volver al mismo link muestra el estado actual de cada asistente y permite corregir.
- Pantalla pública `/capacitador/pase-lista/:token`:
  - Itera asistentes **uno por uno** en orden alfabético (`Apellidos`, luego `Nombres`).
  - Muestra nombre + identificación + componente `AttendanceToggle`.
  - Botones "Anterior" / "Siguiente". Permite avanzar solo si el asistente actual está marcado o saltarse manualmente.
  - Al marcar el último y avanzar, muestra `swal2` indicando "Pase de lista completado".

**Componente `AttendanceToggle`** (reusable entre pantalla pública y admin):
- Split button tipo radio con botones `Presente` y `Ausente`.
- Estado inicial (`null`): ambos en **gris** (neutral).
- `Presente` seleccionado: `bg-success` (verde); `Ausente` seleccionado: `bg-danger` (rojo).
- Marcar uno desmarca el otro. **No se puede desmarcar ambos** una vez que alguno ha sido seleccionado.

**Admin — corrección de asistencia:**
- En `/capacitaciones/{id}/asistentes`, cada fila muestra `AttendanceToggle` permitiendo cambiar la marcación.
- Endpoint admin `PUT /api/capacitaciones/{id}/asistentes/{asistenteId}/asistencia` con body `{ estadoAsistencia: "Presente" | "Ausente" | null }`.

**Endpoint público (token `PaseLista`):**
- `GET /api/capacitador/pase-lista/{token}` — devuelve lista ordenada de asistentes con estado actual.
- `PUT /api/capacitador/pase-lista/{token}/asistentes/{asistenteId}` — body `{ estadoAsistencia }`.

### 7.10 Calificaciones (Fase 11)

**Entidad `Asistente` extendida:**
- `Calificacion` (decimal(4,2), nullable 0–10, step 0.1) — aplica solo cuando la capacitación es `Aprobacion`.

**Link del capacitador — calificaciones:**
- Endpoint admin `POST /api/capacitaciones/{id}/link-calificaciones` emite JWT con `role=Calificaciones`. Solo válido si la capacitación es `TipoCertificacion == Aprobacion`.
- Pantalla pública `/capacitador/calificaciones/:token`:
  - Tabla de asistentes con input numérico 0–10 step 0.1 por fila.
  - Muestra `PuntajeMinimo` de la capacitación y resalta (verde/rojo) si la calificación ingresada aprueba o no.
  - Solo considera asistentes con `EstadoAsistencia == Presente`.

**Admin — edición en línea:**
- En `/capacitaciones/{id}/asistentes`, columna `Calificación` editable (mismos límites).

**Endpoints:**
- Público: `GET /api/capacitador/calificaciones/{token}`, `PUT /api/capacitador/calificaciones/{token}/asistentes/{asistenteId}` con `{ calificacion }`.
- Admin: `PUT /api/capacitaciones/{id}/asistentes/{asistenteId}/calificacion`.

### 7.11 Lógica condicional de certificado (Fase 12)

**Reglas universales** aplicadas en `GenerarCertificadosCapacitacionUseCase`:
- `EstadoAsistencia == Ausente` → **sin certificado** (se omite en la emisión).
- `EstadoAsistencia == null` → sin certificado (tratado como no registrado).
- `TipoCertificacion == Participacion` + `EstadoAsistencia == Presente` → certificado de **Participación**.
- `TipoCertificacion == Aprobacion` + `Presente` + `Calificacion >= PuntajeMinimo` → certificado de **Aprobación**.
- `TipoCertificacion == Aprobacion` + `Presente` + `Calificacion < PuntajeMinimo` (o `null`) → certificado de **Asistencia** (nuevo valor efectivo, no se cambia el tipo en la capacitación).

**Payload al `emisor_documentos`** — se agrega:
```json
{
  "capacitacion": {
    ...
    "logoUrlInterna": "file:///imagen_capacitaciones/<guid>.png"
  },
  "asistente": {
    ...
    "calificacion": 8.5
  },
  "certificadoEfectivo": "Aprobacion" | "Participacion" | "Asistencia"
}
```

**Plantilla `certificado.html`:**
- Nuevo placeholder `{{capacitacion.logo}}` — bloque `<img>` condicional.
- Texto dinámico según `certificadoEfectivo` (no según `tipoCertificacion` original).
- Si `Aprobacion` efectiva, puede mostrar "con calificación de [Calificacion]/[PuntajeMinimo]" (a confirmar en fase 12).

## 8. Estado actual

- [x] Design system en `./style/` listo para consumo.
- [x] `instrucciones.md` con alcance v1 (incluido certificados).
- [x] Repo Git inicializado y publicado en `github.com/pasthortown/capacitaciones`.
- [x] Scaffolding Backend (.NET 8 hexagonal) — Domain/Application/Infrastructure/Api + tests xUnit.
- [x] Scaffolding Frontend (React + Vite + layout Sidebar/Body + Modal UX + `http.js`).
- [x] `docker-compose.yml` + red `capacitaciones-net` + nginx confs + Dockerfile backend + `.env.example`.
- [x] Módulo Catálogos (Modalidad, TipoActividad, Área) — CRUD + XLSX.
- [x] Login admin (JWT + BCrypt) + protección de endpoints admin.
- [x] Pantalla Configuración de numeración (`/configuracion/numeracion`).
- [x] Módulo Capacitaciones — CRUD + grid de cards + gestión de responsables + `SignaturePad`.
- [x] Página capacitador (link firmado, descripción + firma + cargo + empresa).
- [x] Página pública de inscripción + componente `SignaturePad`.
- [x] Listado de asistentes por capacitación + descarga real de certificado.
- [x] Servicio `emisor_documentos` (Node + Puppeteer) + plantilla HTML del certificado.
- [x] Módulo Repositorio — upload/CRUD de material (≤100 MB), volumen `./repository`, contenedor `repository_httpd`, link público de descarga.
- [x] Fase 9 — Puntaje mínimo + Logo de capacitación (volumen `imagen_capacitaciones`).
- [x] Fase 10 — Pase de lista (link capacitador + `AttendanceToggle` admin).
- [x] Fase 11 — Calificaciones (link capacitador + edición admin).
- [x] Fase 12 — Lógica condicional de certificado (Aprobación / Asistencia / Participación + logo en plantilla + filtro de ausentes).
- [ ] Integración SonarQube + OWASP ZAP.

## 9. Plan de fases

| Fase | Objetivo                                                                                  | Agentes involucrados          |
| ---- | ----------------------------------------------------------------------------------------- | ----------------------------- |
| 0    | ✅ Bootstrap: scaffolds front/back + compose + nginx                                      | Backend, Frontend, Infra      |
| 1    | ✅ Catálogos (Modalidad, TipoActividad, Área) + import/export XLSX                        | Backend, Frontend             |
| 2    | ✅ Login admin (JWT) + Configuración de numeración                                        | Backend, Frontend             |
| 3    | ✅ CRUD Capacitaciones + grid de cards + gestión de responsables + `SignaturePad`         | Backend, Frontend             |
| 4    | ✅ Link capacitador (descripción + firma + cargo + empresa, token firmado)                | Backend, Frontend             |
| 5    | ✅ Página pública de inscripción + `SignaturePad` + listado de asistentes                  | Backend, Frontend             |
| 6    | ✅ Servicio `emisor_documentos` + plantilla HTML + integración con backend (endpoint gen.) | Infra, Backend                |
| 7    | ✅ Módulo Repositorio (CRUD + httpd storage + link público, blacklist ejec/scripts, ≤100MB) | Infra, Backend, Frontend     |
| 9    | ✅ Puntaje mínimo + Logo capacitación (volumen `imagen_capacitaciones`)                   | Infra, Backend, Frontend      |
| 10   | ✅ Pase de lista (link capacitador + `AttendanceToggle` admin)                            | Backend, Frontend             |
| 11   | ✅ Calificaciones (link capacitador + edición admin)                                      | Backend, Frontend             |
| 12   | ✅ Lógica condicional de certificado + logo en plantilla                                  | Backend, emisor_documentos    |
| 8    | Pasada de calidad y seguridad (Sonar + ZAP)                                               | Security                      |

## 10. Decisiones tomadas (v1)

| # | Tema               | Resolución                                                                                     |
| - | ------------------ | ---------------------------------------------------------------------------------------------- |
| 1 | Duración           | `DuracionMinutos` múltiplo de 30; UI con inputs de horas y minutos, step de 30.                |
| 2 | Código             | `CAP-PC-REG-###` con 3 dígitos fijos (`001`…`999`).                                            |
| 3 | Autenticación      | Login obligatorio para admin. Capacitador e inscripción pública por link con token firmado.    |
| 4 | Capacitador        | Texto libre — `varchar(255)`. No es catálogo.                                                  |
| 5 | Estado             | Derivado: `Inscripciones Abiertas` → `Iniciada` (al `FechaHoraInicio`) → `Finalizada` (al fin).|
| 6 | SQL Server         | Imagen `mcr.microsoft.com/mssql/server:2022-latest` con `MSSQL_PID=Express`.                   |
| 7 | Puntaje Aprobación | `PuntajeMinimo` decimal 0–10, step 0.1, requerido solo si `TipoCertificacion==Aprobacion`.     |
| 8 | Logo capacitación  | Archivo físico en volumen `./imagen_capacitaciones/` (mismo patrón RW/RO que Repositorio), servido por `repository_httpd` bajo `/imagenes/`. Whitelist `png,jpg,jpeg,webp,svg`, ≤2MB. |
| 9 | Links capacitador  | 3 URLs firmadas **separadas**: descripción/firma, pase de lista, calificaciones. No un solo link con tabs. |
| 10| Ausentes           | Sin certificado, independientemente del `TipoCertificacion`.                                   |
| 11| Cert. efectivo     | `Aprobacion` + `Calificacion < PuntajeMinimo` → cert. tipo **Asistencia** (sin mutar el tipo original de la capacitación). |
| 12| AttendanceToggle   | Split button radio (gris → verde/rojo), no desmarcable ambos. Reusado en pantalla pública y tabla admin. |
