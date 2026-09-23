import smtplib

from fastapi.testclient import TestClient

import app as app_module

client = TestClient(app_module.app)

BODY = {
    "recipient": "admin@dos.com.ec",
    "smtp": {"host": "smtp.x.local", "port": 587, "useTls": True, "from": "r@dos.com.ec", "fromName": "CapacitaDOS"},
}


def test_send_test_ok_usa_la_config_recibida(monkeypatch):
    calls = []
    monkeypatch.setattr(app_module, "send_via_smtp", lambda m, r, cfg: calls.append((m, r, cfg)))

    r = client.post("/send-test", json=BODY)

    assert r.status_code == 200
    assert r.json() == {"ok": True, "error": None}
    message, recipients, cfg = calls[0]
    assert recipients == ["admin@dos.com.ec"]
    assert cfg["host"] == "smtp.x.local"
    assert cfg["password"] == ""
    assert "Prueba de configuración" in message["Subject"]


def test_send_test_devuelve_error_smtp(monkeypatch):
    def fail(m, r, cfg):
        raise smtplib.SMTPAuthenticationError(535, b"5.7.3 Authentication unsuccessful")

    monkeypatch.setattr(app_module, "send_via_smtp", fail)

    r = client.post("/send-test", json=BODY)

    assert r.status_code == 200
    assert r.json()["ok"] is False
    assert "535" in r.json()["error"]


def test_send_test_error_smtp_legible(monkeypatch):
    def fail(m, r, cfg):
        raise smtplib.SMTPDataError(554, b"5.2.252 SendAsDenied; a@dos.com.ec not allowed to send as b@dos.com.ec")

    monkeypatch.setattr(app_module, "send_via_smtp", fail)

    r = client.post("/send-test", json=BODY)

    assert r.json()["error"] == "554 5.2.252 SendAsDenied; a@dos.com.ec not allowed to send as b@dos.com.ec"


def test_describir_error_smtp_errores_sin_codigo():
    assert app_module.describir_error_smtp(OSError("Connection refused")) == "Connection refused"
