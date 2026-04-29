import base64
import os
import smtplib
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


def load_logo_base64() -> str:
    path = os.path.join(ASSETS_DIR, "logo.png")
    try:
        with open(path, "rb") as f:
            return base64.b64encode(f.read()).decode("ascii")
    except FileNotFoundError:
        return ""


def load_smtp_config() -> Dict[str, str]:
    required = ["SMTP_HOST", "SMTP_PORT", "SMTP_FROM", "SMTP_PASSWORD"]
    missing = [k for k in required if not os.environ.get(k)]
    if missing:
        raise RuntimeError(f"Variables SMTP faltantes en el entorno: {', '.join(missing)}")
    return {
        "host": os.environ["SMTP_HOST"],
        "port": os.environ["SMTP_PORT"],
        "from": os.environ["SMTP_FROM"],
        "password": os.environ["SMTP_PASSWORD"],
        "use_tls": os.environ.get("SMTP_USE_TLS", "true"),
    }


jinja_env = Environment(
    loader=FileSystemLoader(TEMPLATES_DIR),
    autoescape=select_autoescape(["html", "xml"]),
)
jinja_env.globals["logo_base64"] = load_logo_base64()


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
    sender = cfg["from"]
    password = cfg["password"]
    use_tls = cfg["use_tls"].lower() == "true"

    with smtplib.SMTP(host, port, timeout=30) as server:
        server.ehlo()
        if use_tls:
            server.starttls()
            server.ehlo()
        server.login(sender, password)
        server.sendmail(sender, recipients, message.as_string())


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
    sender = cfg["from"]

    message = build_message(sender, request, html_body)

    all_recipients = list(request.recipients)
    if request.cc:
        all_recipients += list(request.cc)
    if request.bcc:
        all_recipients += list(request.bcc)

    try:
        send_via_smtp(message, all_recipients)
    except smtplib.SMTPException as exc:
        raise HTTPException(status_code=502, detail=f"Error SMTP: {exc}")

    return SendMailResponse(
        status="sent",
        template=request.template,
        recipients=[str(r) for r in request.recipients],
        has_attachment=request.attachment is not None,
    )
