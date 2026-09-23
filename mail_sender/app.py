import base64
import logging
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
from jinja2 import Environment, FileSystemLoader, StrictUndefined, TemplateNotFound, select_autoescape
from jinja2.sandbox import SandboxedEnvironment
from pydantic import BaseModel, ConfigDict, EmailStr, Field, ValidationError

from config_provider import get_remote_config

logging.basicConfig(level=logging.INFO)

TEMPLATES_DIR = os.environ.get("TEMPLATES_DIR", "plantillas")
ASSETS_DIR = os.environ.get("ASSETS_DIR", "assets")

# Reintentos del envío SMTP. El connector/relay puede fallar por parpadeos de DNS
# (`socket.gaierror: Temporary failure in name resolution`) o cortes transitorios de
# red, que NO son `smtplib.SMTPException` sino `OSError`. Reintentamos con backoff
# lineal antes de devolver error para que un parpadeo no impida el envío del correo.
SMTP_MAX_RETRIES = int(os.environ.get("SMTP_MAX_RETRIES", "3"))
SMTP_RETRY_BACKOFF_SECONDS = float(os.environ.get("SMTP_RETRY_BACKOFF_SECONDS", "2"))

log = logging.getLogger("mail_sender")


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


class SmtpSettings(BaseModel):
    """Configuración SMTP explícita (del backend o de /send-test). Claves camelCase del backend."""

    model_config = ConfigDict(populate_by_name=True)

    host: str
    port: int
    user: Optional[str] = None
    password: Optional[str] = None
    use_tls: bool = Field(default=True, alias="useTls")
    from_email: str = Field(alias="from")
    from_name: Optional[str] = Field(default=None, alias="fromName")


def smtp_settings_to_cfg(s: SmtpSettings) -> Dict[str, str]:
    """Convierte a la misma forma que devuelve `load_smtp_config()`."""
    return {
        "host": s.host,
        "port": str(s.port),
        "from_email": s.from_email,
        "from_name": (s.from_name or "").strip(),
        "user": (s.user or "").strip() or s.from_email,
        "password": s.password or "",
        "use_tls": "true" if s.use_tls else "false",
    }


def resolve_smtp_config(remote: Optional[Dict[str, Any]]) -> Dict[str, str]:
    """SMTP de la BD si está configurado y la contraseña es legible; si no, el del entorno (.env)."""
    if remote and remote.get("configurado") and remote.get("smtp"):
        if remote.get("passwordInvalida"):
            log.warning("La contraseña SMTP guardada no se puede descifrar; se usa la configuración del .env.")
        else:
            try:
                return smtp_settings_to_cfg(SmtpSettings.model_validate(remote["smtp"]))
            except ValidationError as exc:
                log.warning("Configuración SMTP del backend inválida (%s); se usa la del .env.", exc)
    return load_smtp_config()


# `SandboxedEnvironment`: el asunto personalizado viene de la BD (lo escribe un admin
# desde la app); sin sandbox, Jinja2 permite escapar al `object` base y ejecutar código
# arbitrario (p. ej. `{{ ''.__class__.__mro__[1].__subclasses__() }}`). `SecurityError`
# (subclase de `TemplateError`, ya cubierta por el `except Exception` de abajo) hace que
# se use el asunto original en vez de fallar el envío.
subject_env = SandboxedEnvironment(undefined=StrictUndefined, autoescape=False)


def resolve_subject(original: str, custom: Optional[str], parameters: Dict[str, Any]) -> str:
    """Renderiza el asunto personalizado con los parámetros del correo; ante error, el original."""
    if not custom or not custom.strip():
        return original
    try:
        rendered = subject_env.from_string(custom).render(**{**parameters, "asunto_original": original})
    except Exception as exc:
        log.warning("Asunto personalizado inválido (%r): %s. Se usa el original.", custom, exc)
        return original
    rendered = " ".join(rendered.split())  # sin saltos de línea en el header Subject
    return rendered or original


def merge_addresses(own: Optional[List[str]], extra: Optional[List[str]], exclude: List[str]) -> List[str]:
    """Une dos listas sin duplicados (sin distinguir mayúsculas) y sin direcciones de `exclude`."""
    seen = {str(e).strip().lower() for e in exclude}
    result: List[str] = []
    for addr in list(own or []) + list(extra or []):
        value = str(addr).strip()
        if value and value.lower() not in seen:
            seen.add(value.lower())
            result.append(value)
    return result


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
    to: List[str],
    subject: str,
    html_body: str,
    cc: List[str],
    attachment: Optional[Attachment],
) -> MIMEMultipart:
    msg = MIMEMultipart("mixed")
    msg["Subject"] = subject
    msg["From"] = sender
    msg["To"] = ", ".join(to)
    if cc:
        msg["Cc"] = ", ".join(cc)

    alt = MIMEMultipart("alternative")
    alt.attach(MIMEText(html_body, "html", "utf-8"))
    msg.attach(alt)

    if attachment:
        try:
            data = base64.b64decode(attachment.content_base64, validate=True)
        except Exception as exc:
            raise HTTPException(status_code=400, detail=f"Adjunto base64 inválido: {exc}")

        maintype, _, subtype = (attachment.mime_type or "application/octet-stream").partition("/")
        part = MIMEBase(maintype or "application", subtype or "octet-stream")
        part.set_payload(data)
        encoders.encode_base64(part)
        part.add_header(
            "Content-Disposition",
            f'attachment; filename="{attachment.filename}"',
        )
        msg.attach(part)

    return msg


def send_via_smtp(message: MIMEMultipart, recipients: List[str], cfg: Dict[str, str]) -> None:
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
    remote = get_remote_config()
    regla = ((remote or {}).get("notificaciones") or {}).get(request.template)

    if regla is not None and regla.get("activo") is False:
        log.info("Notificación '%s' desactivada; se omite el envío a %s.", request.template, request.recipients)
        return SendMailResponse(
            status="omitido",
            template=request.template,
            recipients=[str(r) for r in request.recipients],
            has_attachment=request.attachment is not None,
        )

    subject = resolve_subject(request.subject, (regla or {}).get("asunto"), request.parameters)
    # Las plantillas pueden mostrar {{ subject }} en el cuerpo: que coincida con el asunto final.
    parameters = {**request.parameters, "subject": subject}
    html_body = render_template(request.template, parameters)

    cfg = resolve_smtp_config(remote)
    from_header = format_from_header(cfg)

    to = [str(r) for r in request.recipients]
    cc = merge_addresses(request.cc, (remote or {}).get("ccGlobal"), exclude=to)
    bcc = merge_addresses(request.bcc, (remote or {}).get("bccGlobal"), exclude=to + cc)

    message = build_message(from_header, to, subject, html_body, cc, request.attachment)
    all_recipients = to + cc + bcc

    # Reintentos con backoff. Capturamos `OSError` además de `smtplib.SMTPException`
    # porque los fallos de resolución de DNS (`socket.gaierror`) y los cortes de red
    # transitorios derivan de `OSError`, no de `SMTPException`; antes escapaban del
    # `except` y el endpoint devolvía 500 sin enviar el correo.
    last_exc: Optional[BaseException] = None
    for intento in range(1, SMTP_MAX_RETRIES + 1):
        try:
            send_via_smtp(message, all_recipients, cfg)
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
