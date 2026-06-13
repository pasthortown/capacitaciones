import base64
import os
import smtplib
import socket
import time
from email.mime.base import MIMEBase
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email import encoders
from typing import Any, Dict, List, Optional

from fastapi import FastAPI, HTTPException
from jinja2 import Environment, FileSystemLoader, TemplateNotFound, select_autoescape
from pydantic import BaseModel, EmailStr, Field


TEMPLATES_DIR = os.environ.get("TEMPLATES_DIR", "plantillas")
ASSETS_DIR = os.environ.get("ASSETS_DIR", "assets")

# Reintentos del envío SMTP. El connector/relay puede fallar por parpadeos de DNS
# (`socket.gaierror: Temporary failure in name resolution`) o cortes transitorios de
# red, que NO son `smtplib.SMTPException` sino `OSError`. Reintentamos con backoff
# lineal antes de devolver error para que un parpadeo no impida el envío del correo.
SMTP_MAX_RETRIES = int(os.environ.get("SMTP_MAX_RETRIES", "3"))
SMTP_RETRY_BACKOFF_SECONDS = float(os.environ.get("SMTP_RETRY_BACKOFF_SECONDS", "2"))


def load_logo_base64() -> str:
    path = os.path.join(ASSETS_DIR, "logo.png")
    try:
        with open(path, "rb") as f:
            return base64.b64encode(f.read()).decode("ascii")
    except FileNotFoundError:
        return ""


def resolve_logo_src() -> str:
    """
    Resuelve el src del logo a usar en las plantillas:
    - Si LOGO_URL está definida, retorna esa URL pública (se usa cuando el
      sistema esté publicado en un dominio accesible desde clientes de email).
    - En caso contrario, embebe assets/logo.png como data URI base64 (fallback
      útil mientras la app no es pública).
    """
    url = os.environ.get("LOGO_URL", "").strip()
    if url:
        return url
    b64 = load_logo_base64()
    return f"data:image/png;base64,{b64}" if b64 else ""


def load_smtp_config() -> Dict[str, str]:
    """
    Lee la configuración SMTP del entorno.

    Obligatorias: `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM` (email mostrado en el
    header `From` y dirección que firma los mensajes).

    Opcionales:
    - `SMTP_FROM_NAME`: display name del remitente. Si está, el header `From`
      se construye como `"{name} <{email}>"`; si no, va solo el email.
    - `SMTP_USER`: usuario de autenticación SMTP cuando difiere del email del
      `From` (caso típico: alias con permiso *Send As* en Office 365). Si está
      vacío, se autentica con `SMTP_FROM`.
    - `SMTP_PASSWORD`: si está vacío, se omite el `LOGIN` (modo connector MX
      directo, sin auth).
    - `SMTP_USE_TLS=true`: intenta `STARTTLS` y se degrada con gracia si el
      servidor no lo anuncia (`SMTPNotSupportedError`).
    """
    required = ["SMTP_HOST", "SMTP_PORT", "SMTP_FROM"]
    missing = [k for k in required if not os.environ.get(k)]
    if missing:
        raise RuntimeError(f"Variables SMTP faltantes en el entorno: {', '.join(missing)}")
    from_email = os.environ["SMTP_FROM"]
    return {
        "host": os.environ["SMTP_HOST"],
        "port": os.environ["SMTP_PORT"],
        "from_email": from_email,
        "from_name": os.environ.get("SMTP_FROM_NAME", "").strip(),
        "user": os.environ.get("SMTP_USER", "").strip() or from_email,
        "password": os.environ.get("SMTP_PASSWORD", ""),
        "use_tls": os.environ.get("SMTP_USE_TLS", "true"),
    }


def format_from_header(cfg: Dict[str, str]) -> str:
    """Devuelve el header `From` final: `"Display <email>"` si hay nombre, si no solo el email."""
    name = cfg.get("from_name", "")
    email = cfg["from_email"]
    if not name:
        return email
    # `formataddr` se encarga del encoding correcto si el nombre tiene caracteres no-ASCII.
    from email.utils import formataddr
    return formataddr((name, email))


jinja_env = Environment(
    loader=FileSystemLoader(TEMPLATES_DIR),
    autoescape=select_autoescape(["html", "xml"]),
)
jinja_env.globals["logo_src"] = resolve_logo_src()


class Attachment(BaseModel):
    filename: str = Field(..., description="Nombre del archivo adjunto")
    content_base64: str = Field(..., description="Contenido del archivo en base64")
    mime_type: Optional[str] = Field(
        default="application/octet-stream",
        description="MIME type del adjunto",
    )


class SendMailRequest(BaseModel):
    template: str = Field(..., description="Nombre de la plantilla (sin extensión .html)")
    parameters: Dict[str, Any] = Field(
        default_factory=dict,
        description="Parámetros para renderizar la plantilla con Jinja2",
    )
    recipients: List[EmailStr] = Field(..., description="Lista de destinatarios")
    subject: str = Field(..., description="Asunto del correo")
    attachment: Optional[Attachment] = Field(
        default=None,
        description="Adjunto opcional en base64",
    )
    cc: Optional[List[EmailStr]] = Field(default=None, description="Copia (opcional)")
    bcc: Optional[List[EmailStr]] = Field(default=None, description="Copia oculta (opcional)")


class SendMailResponse(BaseModel):
    status: str
    template: str
    recipients: List[str]
    has_attachment: bool


def render_template(template_name: str, parameters: Dict[str, Any]) -> str:
    name = template_name if template_name.endswith(".html") else f"{template_name}.html"
    try:
        template = jinja_env.get_template(name)
    except TemplateNotFound:
        raise HTTPException(status_code=404, detail=f"Plantilla '{name}' no encontrada en {TEMPLATES_DIR}")
    return template.render(**parameters)


def build_message(
    sender: str,
    request: SendMailRequest,
    html_body: str,
) -> MIMEMultipart:
    msg = MIMEMultipart("mixed")
    msg["Subject"] = request.subject
    msg["From"] = sender
    msg["To"] = ", ".join(request.recipients)
    if request.cc:
        msg["Cc"] = ", ".join(request.cc)

    alt = MIMEMultipart("alternative")
    alt.attach(MIMEText(html_body, "html", "utf-8"))
    msg.attach(alt)

    if request.attachment:
        try:
            data = base64.b64decode(request.attachment.content_base64, validate=True)
        except Exception as exc:
            raise HTTPException(status_code=400, detail=f"Adjunto base64 inválido: {exc}")

        maintype, _, subtype = (request.attachment.mime_type or "application/octet-stream").partition("/")
        part = MIMEBase(maintype or "application", subtype or "octet-stream")
        part.set_payload(data)
        encoders.encode_base64(part)
        part.add_header(
            "Content-Disposition",
            f'attachment; filename="{request.attachment.filename}"',
        )
        msg.attach(part)

    return msg


def send_via_smtp(message: MIMEMultipart, recipients: List[str]) -> None:
    cfg = load_smtp_config()
    host = cfg["host"]
    port = int(cfg["port"])
    auth_user = cfg["user"]            # usuario que autentica
    envelope_from = cfg["from_email"]  # MAIL FROM del envelope (suele ser el alias)
    password = cfg["password"]
    use_tls = cfg["use_tls"].lower() == "true"

    # `socket.getfqdn()` permite que el connector MX (Office 365 inbound) vea
    # el FQDN del cliente en el EHLO, igual que las impresoras corporativas.
    fqdn = socket.getfqdn()

    with smtplib.SMTP(host, port, timeout=30) as server:
        server.ehlo(fqdn)
        if use_tls:
            try:
                server.starttls()
                server.ehlo(fqdn)
            except smtplib.SMTPNotSupportedError:
                # El connector MX (puerto 25 sin auth) puede no anunciar
                # STARTTLS — se degrada a envío en claro, comportamiento
                # idéntico al script `send_mail_direct.py` de referencia.
                pass
        if password:
            server.login(auth_user, password)
        server.sendmail(envelope_from, recipients, message.as_string())


app = FastAPI(title="Mail API", version="1.0.0")


@app.get("/health")
def health() -> Dict[str, str]:
    return {"status": "ok"}


@app.get("/templates")
def list_templates() -> Dict[str, List[str]]:
    if not os.path.isdir(TEMPLATES_DIR):
        return {"templates": []}
    items = [f for f in os.listdir(TEMPLATES_DIR) if f.endswith(".html")]
    return {"templates": sorted(items)}


@app.post("/send-mail", response_model=SendMailResponse)
def send_mail(request: SendMailRequest) -> SendMailResponse:
    html_body = render_template(request.template, request.parameters)

    cfg = load_smtp_config()
    from_header = format_from_header(cfg)

    message = build_message(from_header, request, html_body)

    all_recipients = list(request.recipients)
    if request.cc:
        all_recipients += list(request.cc)
    if request.bcc:
        all_recipients += list(request.bcc)

    # Reintentos con backoff. Capturamos `OSError` además de `smtplib.SMTPException`
    # porque los fallos de resolución de DNS (`socket.gaierror`) y los cortes de red
    # transitorios derivan de `OSError`, no de `SMTPException`; antes escapaban del
    # `except` y el endpoint devolvía 500 sin enviar el correo.
    last_exc: Optional[BaseException] = None
    for intento in range(1, SMTP_MAX_RETRIES + 1):
        try:
            send_via_smtp(message, all_recipients)
            last_exc = None
            break
        except (smtplib.SMTPException, OSError) as exc:
            last_exc = exc
            if intento < SMTP_MAX_RETRIES:
                time.sleep(SMTP_RETRY_BACKOFF_SECONDS * intento)

    if last_exc is not None:
        raise HTTPException(
            status_code=502,
            detail=f"Error de envío tras {SMTP_MAX_RETRIES} intentos: {last_exc}",
        )

    return SendMailResponse(
        status="sent",
        template=request.template,
        recipients=[str(r) for r in request.recipients],
        has_attachment=request.attachment is not None,
    )
