"""
Configuración de correo administrada desde la app (Configuración → Correo).

Se lee del backend (`GET {BACKEND_INTERNAL_URL}/api/internal/correo-config`, header
`X-Internal-Key: {MAIL_CONFIG_API_KEY}`) y se cachea `CONFIG_CACHE_SECONDS` segundos.
Si falta alguna de las variables, o el backend no responde y no hay nada cacheado, se
devuelve None y `app.py` usa las variables SMTP_* del entorno como siempre.
"""

import json
import logging
import os
import time
import urllib.request
from typing import Any, Dict, Optional

log = logging.getLogger("mail_sender.config")

CACHE_SECONDS = float(os.environ.get("CONFIG_CACHE_SECONDS", "60"))
FETCH_TIMEOUT_SECONDS = 5

_cache: Dict[str, Any] = {"value": None, "fetched_at": 0.0}


def _fetch_remote() -> Optional[Dict[str, Any]]:
    base = os.environ.get("BACKEND_INTERNAL_URL", "").strip().rstrip("/")
    key = os.environ.get("MAIL_CONFIG_API_KEY", "").strip()
    if not base or not key:
        return None
    req = urllib.request.Request(
        f"{base}/api/internal/correo-config",
        headers={"X-Internal-Key": key, "Accept": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=FETCH_TIMEOUT_SECONDS) as resp:
        return json.loads(resp.read().decode("utf-8"))


def get_remote_config() -> Optional[Dict[str, Any]]:
    """Config del backend (cacheada). Ante un error devuelve la última conocida, aunque esté vencida."""
    now = time.monotonic()
    cached = _cache["value"]
    if cached is not None and now - _cache["fetched_at"] < CACHE_SECONDS:
        return cached

    try:
        value = _fetch_remote()
    except Exception as exc:  # red, HTTP 4xx/5xx, JSON inválido
        log.warning("No se pudo obtener la configuración de correo del backend: %s", exc)
        return cached

    if value is None:
        return None
    _cache["value"] = value
    _cache["fetched_at"] = now
    return value


def reset_cache() -> None:
    _cache["value"] = None
    _cache["fetched_at"] = 0.0
