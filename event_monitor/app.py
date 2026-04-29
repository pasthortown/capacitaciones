"""
event_monitor — daemon que monitorea capacitaciones y dispara correos masivos
a través de mail_sender.

Tres flujos automáticos (uno por tipo de notificación, ver constantes
TIPO_PRE / TIPO_INICIO / TIPO_ENCUESTA):

  1. Pre-evento: 30 min antes del FechaHoraInicio se envía
     `recordatorio_inicio_proximo` a cada asistente. Tipo en mail_control:
     "Recordatorio pre-evento".
  2. Inicio: cuando el evento ya empezó (en los últimos 30 min) se envía
     `recordatorio_evento_iniciado`. Tipo: "Aviso de Inicio".
  3. Post-evento: cuando ya terminó (en las últimas 24 h) se envía
     `encuesta_satisfaccion` con link + QR. Tipo: "Encuesta de Satisfacción".

Anti-duplicación: tabla `mail_control` con índice único sobre (Codigo,
TipoNotificacion). El listado de destinatarios ya enviados se persiste en
`DestinatariosEnviados` (NVARCHAR(MAX), comma-separated) y se va engrosando
de 5 en 5 — al reiniciar el servicio nadie recibe dos veces.

El servicio no expone HTTP. Healthcheck vía `docker logs`.
"""
import io
import logging
import os
import time
from datetime import datetime, timezone
from typing import Iterable, List, Optional, Set, Tuple

import pymssql
import requests
import segno


# ---------- Configuración (env) ----------

SQL_SERVER = os.environ["SQL_SERVER"]
SQL_PORT = int(os.environ.get("SQL_PORT", "1433"))
SQL_DATABASE = os.environ["SQL_DATABASE"]
SQL_USER = os.environ["SQL_USER"]
SQL_PASSWORD = os.environ["SQL_PASSWORD"]

MAIL_SENDER_URL = os.environ.get("MAIL_SENDER_URL", "http://mail_sender:8000")
PUBLIC_BASE_URL = os.environ.get("PUBLIC_BASE_URL", "http://localhost/capacitados").rstrip("/")
EMAIL_DOMAIN = os.environ.get("EMAIL_DOMAIN", "@dos.com.ec")

POLL_INTERVAL_SECONDS = int(os.environ.get("POLL_INTERVAL_SECONDS", "60"))
BATCH_SIZE = int(os.environ.get("BATCH_SIZE", "5"))
WINDOW_PRE_EVENT_MIN = int(os.environ.get("WINDOW_PRE_EVENT_MIN", "30"))
WINDOW_POST_START_MIN = int(os.environ.get("WINDOW_POST_START_MIN", "30"))
# 24 h después de que terminó el evento ya no se reintenta el envío de la encuesta
# (los que faltaron quedaron registrados en mail_control y la próxima vez que el
# servicio inicie se respetará el listado, pero no buscamos eventos antiguos para
# no escanear la BD entera).
WINDOW_POST_END_HOURS = int(os.environ.get("WINDOW_POST_END_HOURS", "24"))

TIPO_PRE = "Recordatorio pre-evento"
TIPO_INICIO = "Aviso de Inicio"
TIPO_ENCUESTA = "Encuesta de Satisfacción"

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger("event_monitor")


# ---------- DB ----------

def db():
    return pymssql.connect(
        server=SQL_SERVER,
        port=SQL_PORT,
        database=SQL_DATABASE,
        user=SQL_USER,
        password=SQL_PASSWORD,
        as_dict=True,
        autocommit=False,
        login_timeout=10,
        timeout=30,
    )


def ensure_schema() -> None:
    """Crea `mail_control` y su índice único si aún no existen."""
    sql = """
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'mail_control')
    BEGIN
        CREATE TABLE mail_control (
            Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_mail_control_Id DEFAULT NEWID() PRIMARY KEY,
            Codigo NVARCHAR(50) NOT NULL,
            NombreCapacitacion NVARCHAR(MAX) NOT NULL,
            TipoNotificacion NVARCHAR(100) NOT NULL,
            DestinatariosEnviados NVARCHAR(MAX) NULL,
            FechaCreacion DATETIME2 NOT NULL CONSTRAINT DF_mail_control_FC DEFAULT SYSUTCDATETIME(),
            FechaActualizacion DATETIME2 NULL
        );
        CREATE UNIQUE INDEX IX_mail_control_Codigo_Tipo ON mail_control (Codigo, TipoNotificacion);
    END
    """
    with db() as conn:
        cur = conn.cursor()
        cur.execute(sql)
        conn.commit()
    log.info("schema mail_control listo")


def find_capacitaciones_pre_event() -> List[dict]:
    """Capacitaciones con FechaHoraInicio en (now, now+30min] y activas."""
    sql = (
        "SELECT c.Id, c.Codigo, c.Tema, c.FechaHoraInicio, c.DuracionMinutos, "
        "       m.Nombre AS Modalidad "
        "FROM Capacitaciones c "
        "LEFT JOIN Modalidad m ON c.ModalidadId = m.Id "
        "WHERE c.Activo = 1 "
        "  AND c.FechaHoraInicio > SYSUTCDATETIME() "
        "  AND c.FechaHoraInicio <= DATEADD(MINUTE, %d, SYSUTCDATETIME())"
    )
    with db() as conn:
        cur = conn.cursor()
        cur.execute(sql, (WINDOW_PRE_EVENT_MIN,))
        return list(cur.fetchall())


def find_capacitaciones_inicio() -> List[dict]:
    """Capacitaciones que iniciaron en los últimos `WINDOW_POST_START_MIN` minutos."""
    sql = (
        "SELECT c.Id, c.Codigo, c.Tema, c.FechaHoraInicio, c.DuracionMinutos, "
        "       m.Nombre AS Modalidad "
        "FROM Capacitaciones c "
        "LEFT JOIN Modalidad m ON c.ModalidadId = m.Id "
        "WHERE c.Activo = 1 "
        "  AND c.FechaHoraInicio <= SYSUTCDATETIME() "
        "  AND c.FechaHoraInicio >= DATEADD(MINUTE, -%d, SYSUTCDATETIME())"
    )
    with db() as conn:
        cur = conn.cursor()
        cur.execute(sql, (WINDOW_POST_START_MIN,))
        return list(cur.fetchall())


def find_capacitaciones_finalizadas() -> List[dict]:
    """Capacitaciones que terminaron en las últimas `WINDOW_POST_END_HOURS` horas."""
    sql = (
        "SELECT c.Id, c.Codigo, c.Tema, c.FechaHoraInicio, c.DuracionMinutos, "
        "       m.Nombre AS Modalidad "
        "FROM Capacitaciones c "
        "LEFT JOIN Modalidad m ON c.ModalidadId = m.Id "
        "WHERE c.Activo = 1 "
        "  AND DATEADD(MINUTE, c.DuracionMinutos, c.FechaHoraInicio) <= SYSUTCDATETIME() "
        "  AND DATEADD(MINUTE, c.DuracionMinutos, c.FechaHoraInicio) >= DATEADD(HOUR, -%d, SYSUTCDATETIME())"
    )
    with db() as conn:
        cur = conn.cursor()
        cur.execute(sql, (WINDOW_POST_END_HOURS,))
        return list(cur.fetchall())


def find_asistentes(capacitacion_id) -> List[dict]:
    sql = (
        "SELECT Id, Nombres, Apellidos, EmailUsuario "
        "FROM Asistentes WHERE CapacitacionId = %s"
    )
    with db() as conn:
        cur = conn.cursor()
        cur.execute(sql, (capacitacion_id,))
        return list(cur.fetchall())


def get_control_row(codigo: str, tipo: str) -> Tuple[Optional[str], Set[str]]:
    """Devuelve (Id, set_de_destinatarios). Id es None si la fila no existe aún."""
    sql = (
        "SELECT Id, DestinatariosEnviados FROM mail_control "
        "WHERE Codigo=%s AND TipoNotificacion=%s"
    )
    with db() as conn:
        cur = conn.cursor()
        cur.execute(sql, (codigo, tipo))
        row = cur.fetchone()
    if row is None:
        return None, set()
    raw = row.get("DestinatariosEnviados") or ""
    emails = {e.strip().lower() for e in raw.split(",") if e.strip()}
    return row["Id"], emails


def upsert_control_row(codigo: str, nombre: str, tipo: str, sent_emails: Set[str]) -> None:
    """Inserta o actualiza la fila en mail_control con el listado completo de enviados."""
    csv = ",".join(sorted(sent_emails))
    update_sql = (
        "UPDATE mail_control SET DestinatariosEnviados=%s, FechaActualizacion=SYSUTCDATETIME() "
        "WHERE Codigo=%s AND TipoNotificacion=%s"
    )
    insert_sql = (
        "INSERT INTO mail_control (Codigo, NombreCapacitacion, TipoNotificacion, DestinatariosEnviados) "
        "VALUES (%s, %s, %s, %s)"
    )
    with db() as conn:
        cur = conn.cursor()
        cur.execute(update_sql, (csv, codigo, tipo))
        if cur.rowcount == 0:
            cur.execute(insert_sql, (codigo, nombre, tipo, csv))
        conn.commit()


# ---------- Helpers ----------

def email_for(asistente: dict) -> str:
    raw = (asistente.get("EmailUsuario") or "").strip()
    if not raw:
        return ""
    return raw if "@" in raw else f"{raw}{EMAIL_DOMAIN}"


def fmt_local(dt: datetime, fmt: str) -> str:
    """Convierte un datetime UTC (naive de SQL) a local (TZ del contenedor)."""
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone().strftime(fmt)


def fmt_fecha(dt: datetime) -> str:
    """`29 de abril de 2026` (TZ local del contenedor)."""
    meses = [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ]
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    local = dt.astimezone()
    return f"{local.day:02d} de {meses[local.month - 1]} de {local.year}"


def chunked(seq: List, n: int) -> Iterable[List]:
    for i in range(0, len(seq), n):
        yield seq[i : i + n]


def qr_png_base64(content: str) -> str:
    """PNG base64 (sin prefijo data URI) usando segno (pure-python)."""
    qr = segno.make(content, error="q")
    buf = io.BytesIO()
    qr.save(buf, kind="png", scale=6, border=2)
    import base64
    return base64.b64encode(buf.getvalue()).decode("ascii")


def send_template(template: str, recipient: str, subject: str, parameters: dict) -> None:
    """Llama POST /send-mail. Lanza excepción si mail_sender devuelve 4xx/5xx."""
    payload = {
        "template": template,
        "recipients": [recipient],
        "subject": subject,
        "parameters": parameters,
    }
    r = requests.post(f"{MAIL_SENDER_URL}/send-mail", json=payload, timeout=120)
    r.raise_for_status()


# ---------- Procesamiento por tipo ----------

def parametros_pre_evento(c: dict, asistente: dict) -> dict:
    return {
        "subject": f"Recordatorio: tu capacitación inicia pronto - {c['Tema']}",
        "nombre": f"{(asistente.get('Nombres') or '').strip()} {(asistente.get('Apellidos') or '').strip()}".strip(),
        "tema": c["Tema"],
        "fecha": fmt_fecha(c["FechaHoraInicio"]),
        "hora": fmt_local(c["FechaHoraInicio"], "%H:%M"),
        "modalidad": c.get("Modalidad") or "",
    }


def parametros_inicio(c: dict, asistente: dict) -> dict:
    return {
        "subject": f"Tu capacitación ya inició: {c['Tema']}",
        "nombre": f"{(asistente.get('Nombres') or '').strip()} {(asistente.get('Apellidos') or '').strip()}".strip(),
        "tema": c["Tema"],
        "modalidad": c.get("Modalidad") or "",
    }


def parametros_encuesta(c: dict, asistente: dict, link: str, qr_b64: str) -> dict:
    return {
        "subject": f"Cuéntanos tu experiencia: {c['Tema']}",
        "nombre": f"{(asistente.get('Nombres') or '').strip()} {(asistente.get('Apellidos') or '').strip()}".strip(),
        "tema": c["Tema"],
        "link": link,
        "qrBase64": qr_b64,
    }


def procesar_capacitacion(
    c: dict,
    tipo: str,
    template: str,
    build_params,
) -> None:
    """
    Despacha en bloques de `BATCH_SIZE` los correos pendientes para una
    capacitación, persistiendo el avance en mail_control después de CADA
    bloque para que un reinicio no duplique envíos.
    """
    codigo = c["Codigo"]
    nombre = c["Tema"]

    asistentes = find_asistentes(c["Id"])
    if not asistentes:
        return

    _, ya_enviados = get_control_row(codigo, tipo)

    pendientes: List[Tuple[dict, str]] = []
    for a in asistentes:
        em = email_for(a)
        if not em:
            continue
        if em.lower() in ya_enviados:
            continue
        pendientes.append((a, em))

    if not pendientes:
        return

    log.info(
        "tipo=%s codigo=%s pendientes=%d/%d (ya enviados=%d)",
        tipo,
        codigo,
        len(pendientes),
        len(asistentes),
        len(ya_enviados),
    )

    for batch in chunked(pendientes, BATCH_SIZE):
        for asistente, email in batch:
            params = build_params(c, asistente)
            try:
                send_template(template, email, params["subject"], params)
                ya_enviados.add(email.lower())
            except Exception as exc:
                log.warning(
                    "tipo=%s codigo=%s email=%s falló envío: %s",
                    tipo,
                    codigo,
                    email,
                    exc,
                )
        # Persistencia incremental: aunque haya errores en algunos, los que
        # SÍ se enviaron quedan registrados antes de seguir al próximo bloque.
        upsert_control_row(codigo, nombre, tipo, ya_enviados)


def ciclo() -> None:
    # 1) Pre-evento (30 min antes)
    for c in find_capacitaciones_pre_event():
        procesar_capacitacion(
            c,
            TIPO_PRE,
            "recordatorio_inicio_proximo",
            parametros_pre_evento,
        )

    # 2) Aviso de Inicio (apenas comenzó)
    for c in find_capacitaciones_inicio():
        procesar_capacitacion(
            c,
            TIPO_INICIO,
            "recordatorio_evento_iniciado",
            parametros_inicio,
        )

    # 3) Encuesta de Satisfacción (terminó hace <= WINDOW_POST_END_HOURS h)
    for c in find_capacitaciones_finalizadas():
        link = f"{PUBLIC_BASE_URL}/encuesta/{c['Id']}"
        qr_b64 = qr_png_base64(link)
        procesar_capacitacion(
            c,
            TIPO_ENCUESTA,
            "encuesta_satisfaccion",
            lambda cap, asis, _link=link, _qr=qr_b64: parametros_encuesta(cap, asis, _link, _qr),
        )


def main() -> None:
    log.info(
        "event_monitor arrancando: poll=%ds, batch=%d, pre=%dmin, inicio=%dmin, encuesta_h=%d",
        POLL_INTERVAL_SECONDS,
        BATCH_SIZE,
        WINDOW_PRE_EVENT_MIN,
        WINDOW_POST_START_MIN,
        WINDOW_POST_END_HOURS,
    )

    # Espera al SQL Server al arrancar (dependencias docker-compose no garantizan
    # que el motor esté listo, solo que el contenedor esté arriba).
    while True:
        try:
            ensure_schema()
            break
        except Exception as exc:
            log.warning("SQL Server aún no responde (%s) — reintentando en 10s", exc)
            time.sleep(10)

    while True:
        try:
            ciclo()
        except Exception:
            log.exception("ciclo de monitoreo falló — se reintenta en el próximo poll")
        time.sleep(POLL_INTERVAL_SECONDS)


if __name__ == "__main__":
    main()
