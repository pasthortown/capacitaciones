# Mail API — Documentación

API REST para envío de correos electrónicos con plantillas Jinja2, soporte de adjuntos en base64 y despliegue dockerizado.

## 1. Descripción general

El servicio expone una API HTTP que permite enviar correos a través de un servidor SMTP (configurado por **variables de entorno** desde el `.env` global del proyecto) usando plantillas HTML renderizadas con Jinja2. Cada solicitud indica:

- El nombre de la plantilla a usar.
- Un objeto con los parámetros que se inyectan en la plantilla.
- Lista de destinatarios.
- Asunto del correo.
- Adjunto opcional codificado en base64.

Las plantillas se almacenan en una carpeta compartida vía volumen Docker, por lo que se pueden agregar, editar o eliminar sin reconstruir la imagen.

## 2. Estructura del proyecto

```
mail_sender/
├── app.py                  # Aplicación FastAPI con los endpoints
├── send_mail.py            # Script standalone (envío directo, sin API)
├── requirements.txt        # Dependencias Python
├── Dockerfile              # Imagen de la API
├── documentacion.md        # Este archivo
└── plantillas/             # Plantillas Jinja2 (.html) — volumen compartido
    └── ejemplo.html
```

> La orquestación vive en el `docker-compose.yml` raíz del proyecto, junto al resto de servicios.

## 3. Configuración SMTP (variables de entorno)

Las credenciales se leen del `.env` global del proyecto y se inyectan al contenedor por `docker-compose.yml`. No hay archivo de credenciales en disco.

| Variable        | Descripción                                              |
|-----------------|----------------------------------------------------------|
| `SMTP_HOST`     | Host del servidor SMTP (ej. `smtp.office365.com`)        |
| `SMTP_PORT`     | Puerto SMTP (587 para STARTTLS)                          |
| `SMTP_FROM`     | Cuenta remitente — también se usa como usuario de login  |
| `SMTP_PASSWORD` | Contraseña de la cuenta remitente                        |
| `SMTP_USE_TLS`  | `true` para usar STARTTLS antes del login                |

Las cinco variables (`SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM`, `SMTP_PASSWORD`) son **obligatorias**; si falta alguna, el endpoint `/send-mail` responde 500 con el detalle de las que faltan. `SMTP_USE_TLS` por defecto es `true`.

## 4. Plantillas Jinja2

Las plantillas se guardan en `plantillas/` con extensión `.html`. Aceptan toda la sintaxis estándar de Jinja2:

- Variables: `{{ nombre }}`
- Filtros con valores por defecto: `{{ titulo | default('Hola') }}`
- Bloques condicionales: `{% if items %}...{% endif %}`
- Bucles: `{% for item in items %}...{% endfor %}`

**Auto-escape** está activado para `.html`, así que las variables se escapan automáticamente para prevenir inyección HTML/XSS.

### Ejemplo incluido (`plantillas/ejemplo.html`)

Variables aceptadas:

| Variable  | Tipo    | Descripción                              |
|-----------|---------|------------------------------------------|
| `titulo`  | string  | Encabezado del correo                    |
| `nombre`  | string  | Nombre del destinatario                  |
| `mensaje` | string  | Cuerpo principal                         |
| `items`   | array   | Lista opcional de viñetas                |
| `enlace`  | string  | URL opcional para botón "Ver más"        |
| `firma`   | string  | Pie de firma                             |

### Agregar una nueva plantilla

1. Crear el archivo `plantillas/mi_plantilla.html`.
2. (Si el contenedor está corriendo) los cambios se reflejan al instante por el volumen montado — **no es necesario reconstruir**.
3. Invocar el endpoint con `"template": "mi_plantilla"` (sin extensión).

## 5. Endpoints de la API

Base URL: `http://localhost:8000`

### `GET /health`

Healthcheck del servicio.

**Respuesta:**
```json
{ "status": "ok" }
```

### `GET /templates`

Lista las plantillas disponibles en la carpeta `plantillas/`.

**Respuesta:**
```json
{ "templates": ["ejemplo.html"] }
```

### `POST /send-mail`

Renderiza la plantilla y envía el correo.

**Body:**

| Campo        | Tipo                     | Requerido | Descripción                                            |
|--------------|--------------------------|-----------|--------------------------------------------------------|
| `template`   | string                   | sí        | Nombre de la plantilla (sin `.html`)                   |
| `parameters` | object                   | no        | Variables para Jinja2 (default `{}`)                   |
| `recipients` | array de emails          | sí        | Lista de destinatarios principales                     |
| `subject`    | string                   | sí        | Asunto del correo                                      |
| `attachment` | objeto                   | no        | Adjunto (ver sub-tabla)                                |
| `cc`         | array de emails          | no        | Copia                                                  |
| `bcc`        | array de emails          | no        | Copia oculta                                           |

**Sub-objeto `attachment`:**

| Campo            | Tipo   | Descripción                                              |
|------------------|--------|----------------------------------------------------------|
| `filename`       | string | Nombre del archivo (con extensión)                       |
| `content_base64` | string | Contenido del archivo en base64                          |
| `mime_type`      | string | MIME type (default `application/octet-stream`)           |

**Respuesta exitosa (200):**
```json
{
  "status": "sent",
  "template": "ejemplo",
  "recipients": ["lasalazar@dos.com.ec"],
  "has_attachment": true
}
```

**Códigos de error:**

| Código | Causa                                              |
|--------|----------------------------------------------------|
| 400    | Adjunto base64 inválido                            |
| 404    | Plantilla no encontrada en `plantillas/`           |
| 422    | Validación del body (emails inválidos, etc.)       |
| 502    | Error SMTP al enviar el correo                     |

### `GET /docs`

Swagger UI interactivo generado automáticamente por FastAPI. Permite probar los endpoints desde el navegador.

## 6. Despliegue con Docker Compose

### Requisitos
- Docker Engine 20.10+
- Docker Compose v2

### Levantar el servicio

```bash
docker compose up -d --build
```

### Ver logs

```bash
docker compose logs -f mail-api
```

### Detener

```bash
docker compose down
```

### Reconstruir tras cambios en `app.py` o `requirements.txt`

```bash
docker compose up -d --build
```

> Cambios en `plantillas/*.html` o en `conf.txt` **no requieren reconstrucción** — ambos están montados como volúmenes.

### Variables de entorno (configurables en `docker-compose.yml`)

| Variable        | Default            | Descripción                        |
|-----------------|--------------------|------------------------------------|
| `CONFIG_PATH`   | `/app/conf.txt`    | Ruta al archivo de credenciales    |
| `TEMPLATES_DIR` | `/app/plantillas`  | Carpeta de plantillas              |
| `TZ`            | `America/Guayaquil`| Zona horaria del contenedor        |

## 7. Ejemplos de uso

### Ejemplo 1 — correo simple sin adjunto

```bash
curl -X POST http://localhost:8000/send-mail \
  -H "Content-Type: application/json" \
  -d '{
    "template": "ejemplo",
    "parameters": {
      "titulo": "Bienvenido",
      "nombre": "Luis",
      "mensaje": "Tu cuenta fue activada correctamente.",
      "firma": "Equipo DOS"
    },
    "recipients": ["lasalazar@dos.com.ec"],
    "subject": "Bienvenido al sistema"
  }'
```

### Ejemplo 2 — correo con lista, enlace y CC

```bash
curl -X POST http://localhost:8000/send-mail \
  -H "Content-Type: application/json" \
  -d '{
    "template": "ejemplo",
    "parameters": {
      "titulo": "Resumen semanal",
      "nombre": "Luis",
      "mensaje": "Estas son las tareas pendientes:",
      "items": ["Revisar reporte Q1", "Aprobar facturas", "Reunión viernes 10am"],
      "enlace": "https://dos.com.ec/dashboard",
      "firma": "Sistema de Tareas"
    },
    "recipients": ["lasalazar@dos.com.ec"],
    "cc": ["supervisor@dos.com.ec"],
    "subject": "Resumen de tareas pendientes"
  }'
```

### Ejemplo 3 — correo con adjunto PDF

```bash
# Codificar el archivo a base64
B64=$(base64 -w0 factura.pdf)

curl -X POST http://localhost:8000/send-mail \
  -H "Content-Type: application/json" \
  -d "{
    \"template\": \"ejemplo\",
    \"parameters\": {
      \"titulo\": \"Factura emitida\",
      \"nombre\": \"Luis\",
      \"mensaje\": \"Adjuntamos su factura del mes.\"
    },
    \"recipients\": [\"lasalazar@dos.com.ec\"],
    \"subject\": \"Factura mes de abril\",
    \"attachment\": {
      \"filename\": \"factura.pdf\",
      \"content_base64\": \"${B64}\",
      \"mime_type\": \"application/pdf\"
    }
  }"
```

### Ejemplo 4 — desde Python

```python
import base64
import requests

with open("documento.pdf", "rb") as f:
    contenido = base64.b64encode(f.read()).decode()

payload = {
    "template": "ejemplo",
    "parameters": {
        "titulo": "Documento adjunto",
        "nombre": "Luis",
        "mensaje": "Por favor revise el documento adjunto.",
        "firma": "Sistema Automático",
    },
    "recipients": ["lasalazar@dos.com.ec"],
    "subject": "Documento para revisión",
    "attachment": {
        "filename": "documento.pdf",
        "content_base64": contenido,
        "mime_type": "application/pdf",
    },
}

r = requests.post("http://localhost:8000/send-mail", json=payload)
print(r.status_code, r.json())
```

## 8. Flujo interno (qué hace la API por dentro)

1. Recibe el JSON y lo valida con Pydantic (formatos de email, campos requeridos).
2. Carga `conf.txt` para obtener credenciales SMTP.
3. Busca la plantilla `{template}.html` en `TEMPLATES_DIR` — si no existe, devuelve 404.
4. Renderiza la plantilla con los `parameters` usando Jinja2 (con auto-escape).
5. Construye un mensaje MIME `multipart/mixed`:
   - Parte HTML con el cuerpo renderizado.
   - Si hay adjunto: decodifica el base64 y lo añade como `MIMEBase` con `Content-Disposition: attachment`.
6. Conecta al SMTP con STARTTLS, hace login y envía a `recipients + cc + bcc`.
7. Devuelve confirmación al cliente.

## 9. Seguridad y consideraciones

- **Credenciales:** `conf.txt` contiene la contraseña SMTP en texto plano. No se debe versionar (añadir a `.gitignore`) y en producción conviene migrar a variables de entorno o un secret manager.
- **Acceso a la API:** el servicio no implementa autenticación. Si se expone fuera de la red local, se debe poner detrás de un reverse proxy con auth (API key, OAuth, mTLS) o restringir el puerto.
- **Validación de adjuntos:** la API valida que el base64 sea decodificable, pero no inspecciona el contenido ni limita tamaño. Para producción considerar imponer un tamaño máximo por adjunto.
- **Auto-escape de Jinja2:** activado para evitar inyección HTML/XSS desde los `parameters`.
- **Rate limiting:** no implementado — agregar si la API se expone públicamente.

## 10. Troubleshooting

| Síntoma                                          | Posible causa / solución                                              |
|--------------------------------------------------|-----------------------------------------------------------------------|
| `404 Plantilla 'xxx.html' no encontrada`         | Verificar que el archivo exista en `plantillas/` y la extensión `.html` |
| `400 Adjunto base64 inválido`                    | Re-codificar el archivo: `base64 -w0 archivo.pdf`                     |
| `502 Error SMTP: 535 Authentication failed`      | Revisar `emailFrom` y `password` en `conf.txt`                        |
| `502 Error SMTP: timeout`                        | Verificar conectividad al puerto 587 desde el contenedor              |
| El correo cae en spam                            | Configurar SPF/DKIM en el dominio remitente                           |
| Cambios en plantilla no se reflejan              | Verificar que el volumen `./plantillas:/app/plantillas` esté montado  |
