# Módulo de Configuración de Correo — Diseño

- **Fecha:** 2026-09-23
- **Estado:** Aprobado en conversación, pendiente revisión del documento
- **Rama:** `feature/configuracion-correo`

## 1. Objetivo

Permitir que un administrador configure desde la aplicación (Configuración → Correo),
sin editar `.env` ni reiniciar contenedores:

1. El servidor SMTP y el remitente de las notificaciones.
2. Activar / desactivar cada tipo de notificación.
3. El asunto de cada tipo de notificación.
4. Direcciones CC / BCC globales que reciben copia de todos los correos.

Además, probar la configuración con un correo de prueba antes de guardarla.

### Fuera de alcance

- Editar el cuerpo HTML de las plantillas.
- Múltiples cuentas / remitentes distintos por tipo de notificación.
- Historial de cambios de configuración (solo se guarda el último `ActualizadoPor/En`).
- Reenvío automático de avisos omitidos al reactivar un tipo.

## 2. Situación actual

- Todos los correos salen por `mail_sender` (FastAPI, `POST /send-mail`), que recibe
  `template`, `subject`, `parameters`, `recipients`, `cc`, `bcc`, `attachment`.
- La configuración SMTP se lee de variables de entorno en `load_smtp_config()`
  (`mail_sender/app.py:52-83`) en cada envío: `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM`,
  `SMTP_FROM_NAME`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_USE_TLS`.
- Llamadores:
  - Backend .NET vía `IMailSenderClient` / `MailSenderHttpClient` (5 casos de uso).
  - `event_monitor` (Python) vía `send_template()`; deduplica con la tabla `mail_control`.

Tipos de notificación existentes (clave = nombre de plantilla):

| Plantilla | Nombre visible | Origen | Asunto actual | Variables disponibles |
|---|---|---|---|---|
| `invitacion_inscripcion` | Invitación de inscripción | Backend | `{tipo} Creado` / `{tipo} Actualizado` / `Invitación a {tipo}: {tema}` | `tema`, `fecha`, `hora`, `duracion`, `modalidad`, `capacitador` |
| `capacitador_descripcion` | Capacitador: cargar información del curso | Backend | `Cargar información del curso: {tema}` | `nombre`, `tema`, `link` |
| `capacitador_pase_lista` | Capacitador: pase de lista | Backend | `Pase de lista: {tema}` | `nombre`, `tema`, `link` |
| `responsable_firma` | Responsable: carga de datos y firma | Backend | `Carga tus datos y firma en CapacitaDOS` | `nombre`, `link` |
| `registro_asistencia_admin` | Reporte de asistencia al admin | Backend | `Registro de asistencia: {tema}` | `tema`, `codigo`, `fecha` |
| `certificado_participante` | Certificado al participante | Backend | `Tu certificado: {tema}` | `nombre`, `tema`, `fecha` |
| `recordatorio_inicio_proximo` | Recordatorio: capacitación por iniciar | event_monitor | `Recordatorio: tu capacitación inicia pronto - {tema}` | `nombre`, `tema`, `fecha`, `hora`, `modalidad` |
| `recordatorio_evento_iniciado` | Aviso: capacitación iniciada | event_monitor | `Tu capacitación ya inició: {tema}` | `nombre`, `tema`, `modalidad` |
| `encuesta_satisfaccion` | Encuesta de satisfacción | event_monitor | `Cuéntanos tu experiencia: {tema}` | `nombre`, `tema`, `link` |

Todas las plantillas reciben además la variable `asunto_original` (el asunto que habría
usado el sistema), útil para `invitacion_inscripcion`, cuyo asunto varía por contexto.

## 3. Enfoque elegido

La configuración vive en SQL Server, se administra a través del backend .NET, y
`mail_sender` la consulta al backend mediante un endpoint interno y la aplica en un solo
lugar. Los llamadores (casos de uso del backend y `event_monitor`) no cambian su forma de
enviar. Si no hay configuración guardada o el backend no responde, `mail_sender` usa las
variables `SMTP_*` del `.env` exactamente como hoy.

Descartados: `mail_sender` leyendo SQL Server directamente (duplica acceso a datos y
descifrado en Python); aplicar reglas en cada llamador (lógica duplicada en ~8 lugares).

## 4. Backend (.NET)

### 4.1 Modelo de datos (migración EF Core, se aplica con `Database.Migrate()` al arrancar)

**`dbo.ConfiguracionCorreo`** — una sola fila (`Id = 1`), creada al primer guardado.

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | int PK | Siempre 1 |
| `SmtpHost` | nvarchar(255) | Requerido |
| `SmtpPort` | int | Requerido, 1–65535 |
| `SmtpUser` | nvarchar(255) null | Si está vacío se usa `RemitenteCorreo` |
| `SmtpPasswordCifrada` | nvarchar(1024) null | AES-GCM, base64 (nonce + tag + ciphertext). Null = sin autenticación (relay) |
| `UsarTls` | bit | Default 1 |
| `RemitenteCorreo` | nvarchar(255) | Requerido, formato email |
| `RemitenteNombre` | nvarchar(255) null | |
| `CcGlobal` | nvarchar(1000) null | Emails separados por coma |
| `BccGlobal` | nvarchar(1000) null | Emails separados por coma |
| `ActualizadoPor` | nvarchar(255) | Email del admin (claim `email`) |
| `ActualizadoEn` | datetime2 | UTC |

**`dbo.ConfiguracionNotificacion`** — una fila por tipo, sembrada con `HasData` con las 9
plantillas de la tabla de la sección 2 (`Activo = 1`, `AsuntoPersonalizado = null`).

| Columna | Tipo | Notas |
|---|---|---|
| `Plantilla` | nvarchar(100) PK | Nombre de la plantilla |
| `Nombre` | nvarchar(200) | Nombre visible |
| `Activo` | bit | |
| `AsuntoPersonalizado` | nvarchar(500) null | Plantilla Jinja; null/vacío = asunto original |
| `ActualizadoPor` | nvarchar(255) null | |
| `ActualizadoEn` | datetime2 null | |

La migración solo crea tablas; no modifica las existentes.

### 4.2 Cifrado de la contraseña

- Servicio `ISecretProtector` (Application/Ports) con implementación `AesGcmSecretProtector`
  (Infrastructure/Security).
- Llave: variable `CORREO_ENCRYPTION_KEY` (32 bytes en base64). Si falta, el backend
  arranca igual pero `PUT` con contraseña nueva responde 500 con mensaje claro y se loguea
  error.
- Si la llave cambia o se pierde, el descifrado falla: el endpoint interno devuelve la
  configuración sin contraseña con `passwordInvalida: true` (mail_sender entonces usa el
  `.env`) y la pantalla muestra aviso "Vuelva a ingresar la contraseña".

### 4.3 Estructura (sigue el patrón de `ConfiguracionNumeracion`)

- Entidades: `Domain/Entities/ConfiguracionCorreo.cs`, `ConfiguracionNotificacion.cs`.
- Configuraciones EF: `Infrastructure/Persistence/Configurations/ConfiguracionCorreoConfiguration.cs`,
  `ConfiguracionNotificacionConfiguration.cs`.
- Puertos/repos: `IConfiguracionCorreoRepository`, `IConfiguracionNotificacionRepository`
  con sus implementaciones en `Infrastructure/Persistence/Repositories/`.
- Casos de uso en `Application/UseCases/Configuracion/`:
  `ObtenerConfiguracionCorreoUseCase`, `ActualizarConfiguracionCorreoUseCase`,
  `ListarNotificacionesUseCase`, `ActualizarNotificacionesUseCase`,
  `EnviarCorreoPruebaUseCase`, `ObtenerConfiguracionCorreoInternaUseCase`.
- DTOs en `Application/Dtos/Configuracion/`.

### 4.4 Endpoints de administración (`ConfiguracionController`, política `Admin`)

- `GET /api/configuracion/correo` → configuración con `tienePassword: bool` (nunca la
  contraseña) y `configurado: bool` (false si la fila no existe → la UI muestra que se usa
  el `.env`).
- `PUT /api/configuracion/correo` → guarda. Reglas:
  - `password` ausente o vacío → se conserva la actual.
  - `quitarPassword: true` → pone `SmtpPasswordCifrada = null` (modo relay).
  - Validaciones: host requerido, puerto 1–65535, remitente email válido, cada CC/BCC email
    válido (máx. 20 por campo). Error → 400 con mensaje por campo.
- `GET /api/configuracion/correo/notificaciones` → lista de 9 tipos con `plantilla`,
  `nombre`, `activo`, `asuntoPersonalizado`, `variables` (lista fija por plantilla,
  sección 2, más `asunto_original`).
- `PUT /api/configuracion/correo/notificaciones` → recibe la lista completa; actualiza solo
  `Activo` y `AsuntoPersonalizado` de plantillas existentes (plantilla desconocida → 400).
  Asunto máx. 500 caracteres.
- `POST /api/configuracion/correo/prueba` → recibe los mismos campos que el `PUT` (si
  `password` viene vacío se usa la guardada) y llama a `mail_sender POST /send-test` con
  destinatario = email del admin logueado. Devuelve `{ ok: bool, mensaje: string }`
  (200 en ambos casos; el error SMTP va en `mensaje`).

### 4.5 Endpoint interno

- `GET /api/internal/correo-config`, `[AllowAnonymous]` + filtro que exige el header
  `X-Internal-Key` igual a `MAIL_CONFIG_API_KEY` (comparación en tiempo constante). Si la
  variable no está configurada, el endpoint responde siempre 403.
- Respuesta: `{ configurado, smtp: {host, port, user, password, useTls, from, fromName},
  ccGlobal: [], bccGlobal: [], notificaciones: { "<plantilla>": {activo, asunto} } }`,
  con la contraseña descifrada. `smtp` es null si `configurado = false`.
- nginx bloquea `location ^~ /capacitados/api/internal/ { return 403; }` (y el equivalente
  sin prefijo si aplica) en `locations-capacitados.inc`.

### 4.6 Certificados omitidos

- `EstadoEnvioCertificado` agrega `Omitido = 4`.
- `IMailSenderClient.SendAsync` pasa a devolver `MailSendResult { Enviado | Omitido }`,
  leyendo `status` de la respuesta de `mail_sender`.
- `GenerarYEnviarCertificadosUseCase` marca `Omitido` (sin mensaje de error) cuando el
  resultado es `Omitido`. Los demás casos de uso ignoran el resultado.
- `reintentar-errores` no toca los `Omitido`.

## 5. mail_sender (Python)

### 5.1 Obtención de configuración

- Nuevas variables: `BACKEND_INTERNAL_URL` (ej. `http://backend:8080`) y
  `MAIL_CONFIG_API_KEY`. Si alguna falta, se usa solo el `.env` (comportamiento actual).
- Nuevo módulo `config_provider.py`: `get_runtime_config()` consulta
  `GET {BACKEND_INTERNAL_URL}/api/internal/correo-config` con timeout de 5 s y cachea el
  resultado 60 s (`CONFIG_CACHE_SECONDS`, configurable).
- Fallback, con log `WARNING`:
  - Backend caído / error HTTP → último valor cacheado si existe (aunque esté vencido);
    si no, `.env`.
  - `configurado = false` o `passwordInvalida = true` → SMTP desde `.env`; las reglas de
    notificaciones (activo/asunto) y CC/BCC del backend se aplican igual si llegaron.

### 5.2 Procesamiento de `POST /send-mail`

1. Busca la regla por `template`. Plantilla sin regla → se envía normal.
2. `activo = false` → no envía; responde `200 {"status": "omitido"}`; log `INFO`.
3. Asunto personalizado no vacío → se renderiza con Jinja (entorno con `StrictUndefined`)
   usando `parameters` + `asunto_original`. Error de renderizado → se usa el asunto
   original y log `WARNING`.
4. CC/BCC globales se unen a los de la solicitud, sin duplicados (comparación sin
   distinguir mayúsculas), y excluyendo direcciones que ya son destinatarios.
5. Envío con la configuración SMTP resuelta y la lógica actual de reintentos.
   Éxito → `200 {"status": "enviado"}` (se conservan los campos que ya devuelve hoy).

`event_monitor` no cambia: un `200` de omitido cuenta como procesado y se registra en
`mail_control`, por lo que al reactivar un tipo no se envían avisos atrasados.

### 5.3 `POST /send-test`

- Body: `{ smtp: {...}, recipient }`. No usa caché ni reglas.
- Envía la nueva plantilla `plantillas/prueba_configuracion.html` (asunto
  "Prueba de configuración de correo — CapacitaDOS") con un solo intento.
- Respuesta: `{ ok: true }` o `{ ok: false, error: "<mensaje SMTP>" }` (siempre 200).
- Solo accesible dentro de la red Docker (el puerto 8000 no está publicado).

### 5.4 Documentación

Actualizar `mail_sender/documentacion.md` (sección SMTP: `SMTP_USER`, `SMTP_FROM_NAME`,
nuevas variables, precedencia BD → `.env`) y `.env.example`.

## 6. Frontend (React)

- Ruta `/configuracion/correo` en `App.jsx`; enlace **Correo** en el grupo Configuración de
  `Sidebar.jsx` (icono `Mail` de lucide-react).
- `pages/configuracion/CorreoPage.jsx`: contenedor con pestañas (patrón `NumeracionPage`).
- `CorreoServidorTab.jsx`:
  - Campos: servidor, puerto, usuario, contraseña (placeholder "•••• (sin cambios)" si
    `tienePassword`), checkbox "Sin contraseña (relay)", checkbox TLS, correo remitente,
    nombre remitente, CC global, BCC global.
  - Banner si `configurado = false`: "Actualmente se usa la configuración del servidor
    (.env). Al guardar, esta configuración la reemplaza."
  - Banner si `passwordInvalida`: "Vuelva a ingresar la contraseña".
  - Botón **Enviar correo de prueba** (usa valores del formulario; muestra resultado y
    error SMTP).
  - Botón **Guardar** con modal de confirmación; toast de éxito/error; errores 400 por
    campo.
- `CorreoNotificacionesTab.jsx`: tabla de 9 filas con nombre, interruptor Activo, campo
  Asunto personalizado (placeholder = asunto actual) y lista de variables como chips.
  Botón **Guardar cambios** (un solo `PUT`).
- Certificados: mostrar el estado `Omitido` con etiqueta propia donde hoy se muestran
  Pendiente/Enviado/Error.
- `services/configuracion.js`: `getCorreo`, `updateCorreo`, `getNotificaciones`,
  `updateNotificaciones`, `enviarCorreoPrueba`.

## 7. Pruebas

- **Backend (`Capacitaciones.Tests`):** cifrado ida y vuelta y fallo con otra llave;
  validaciones del `PUT`; conservación de la contraseña cuando no viene; `quitarPassword`;
  endpoint interno rechaza sin/con llave incorrecta; certificado marcado `Omitido`.
- **mail_sender (pytest, nuevo `tests/`):** fallback a `.env` (backend caído, no
  configurado, contraseña inválida); caché de 60 s; aviso desactivado → `omitido` sin
  llamar a SMTP; asunto renderizado y fallback ante error; unión de CC/BCC sin duplicados;
  `/send-test` devuelve el error SMTP. SMTP simulado con mock.
- **Manual en producción:** correo de prueba, desactivar/activar un tipo, cambiar un
  asunto, verificar 403 externo en `/capacitados/api/internal/`.

## 8. Despliegue

1. Comparar `/Proyectos/RegistroCapacitaciones` con el repositorio (no es un checkout git)
   y revisar diferencias con el usuario antes de sobrescribir.
2. Respaldos con fecha: carpeta del proyecto (incluye `.env`), base de datos de
   `capacitaciones-sqlserver`, `/Docker/web/html/capacitados`, `locations-capacitados.inc`.
3. Agregar al `.env`: `CORREO_ENCRYPTION_KEY`, `MAIL_CONFIG_API_KEY`,
   `BACKEND_INTERNAL_URL` (valores aleatorios). Mantener las `SMTP_*`.
4. `docker compose up -d --build backend mail_sender` en la carpeta del proyecto. No tocar
   contenedores de otros proyectos.
5. `npm run build` del frontend y copiar a `/Docker/web/html/capacitados`.
6. Agregar el bloqueo de `/api/internal/` y `nginx -s reload` en `web-nginx`.
7. Verificación (sección 7, manual) y revisión de logs de `mail_sender` y `event_monitor`.

Cada paso que toque producción se confirma con el usuario antes de ejecutarlo.

**Rollback:** restaurar respaldos y reiniciar `backend` y `mail_sender`. La migración es
aditiva; no requiere revertirse (el enum `Omitido` no afecta filas existentes).

**Git:** rama `feature/configuracion-correo`; push/PR al repositorio remoto solo a pedido
del usuario.
