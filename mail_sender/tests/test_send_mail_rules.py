import pytest
from fastapi.testclient import TestClient

import app as app_module

client = TestClient(app_module.app)

REMOTE = {
    "configurado": True,
    "passwordInvalida": False,
    "smtp": {
        "host": "smtp.db.local", "port": 587, "user": None, "password": "p",
        "useTls": True, "from": "db@dos.com.ec", "fromName": "CapacitaDOS",
    },
    "ccGlobal": ["copia@dos.com.ec", "DESTINO@dos.com.ec"],
    "bccGlobal": ["oculta@dos.com.ec"],
    "notificaciones": {
        "certificado_participante": {"activo": True, "asunto": "[DOS] {{ tema }} | {{ asunto_original }}"},
        "encuesta_satisfaccion": {"activo": False, "asunto": None},
        "responsable_firma": {"activo": True, "asunto": "{{ variable_que_no_existe }}"},
    },
}


@pytest.fixture
def sent(monkeypatch):
    calls = []

    def fake_send(message, recipients, cfg):
        calls.append({"message": message, "recipients": recipients, "cfg": cfg})

    monkeypatch.setattr(app_module, "send_via_smtp", fake_send)
    return calls


def post(template, subject="Original", params=None, cc=None):
    body = {
        "template": template,
        "subject": subject,
        "recipients": ["destino@dos.com.ec"],
        "parameters": params or {"nombre": "Ana", "tema": "Excel", "fecha": "1", "link": "x"},
    }
    if cc:
        body["cc"] = cc
    return client.post("/send-mail", json=body)


def test_desactivado_no_envia_y_responde_omitido(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    r = post("encuesta_satisfaccion")
    assert r.status_code == 200
    assert r.json()["status"] == "omitido"
    assert sent == []


def test_asunto_personalizado_y_copias_globales(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    r = post("certificado_participante", subject="Tu certificado: Excel", cc=["copia@dos.com.ec"])

    assert r.json()["status"] == "sent"
    msg = sent[0]["message"]
    assert msg["Subject"] == "[DOS] Excel | Tu certificado: Excel"
    # CC sin duplicados y sin repetir al destinatario (comparación sin mayúsculas).
    assert msg["Cc"] == "copia@dos.com.ec"
    assert sorted(sent[0]["recipients"]) == ["copia@dos.com.ec", "destino@dos.com.ec", "oculta@dos.com.ec"]
    assert sent[0]["cfg"]["host"] == "smtp.db.local"
    assert sent[0]["cfg"]["from_email"] == "db@dos.com.ec"


def test_asunto_con_error_usa_el_original(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    post("responsable_firma", subject="Carga tus datos")
    assert sent[0]["message"]["Subject"] == "Carga tus datos"


def test_sin_config_remota_usa_env(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: None)
    r = post("certificado_participante", subject="Tu certificado: Excel")
    assert r.json()["status"] == "sent"
    assert sent[0]["cfg"]["host"] == "smtp.env.local"
    assert sent[0]["message"]["Subject"] == "Tu certificado: Excel"


def test_password_invalida_usa_smtp_env_pero_aplica_reglas(monkeypatch, sent):
    remote = {**REMOTE, "passwordInvalida": True}
    monkeypatch.setattr(app_module, "get_remote_config", lambda: remote)
    post("certificado_participante", subject="Tu certificado: Excel")
    assert sent[0]["cfg"]["host"] == "smtp.env.local"
    assert sent[0]["message"]["Subject"].startswith("[DOS] Excel")


def test_plantilla_sin_regla_se_envia_normal(monkeypatch, sent):
    monkeypatch.setattr(app_module, "get_remote_config", lambda: REMOTE)
    r = post("ejemplo", subject="Hola", params={})
    assert r.json()["status"] == "sent"
    assert sent[0]["message"]["Subject"] == "Hola"


def test_asunto_con_intento_de_escape_del_sandbox_usa_el_original(monkeypatch, sent):
    remote = {
        **REMOTE,
        "notificaciones": {
            **REMOTE["notificaciones"],
            "certificado_participante": {
                "activo": True,
                "asunto": "{{ ''.__class__.__mro__[1].__subclasses__() }}",
            },
        },
    }
    monkeypatch.setattr(app_module, "get_remote_config", lambda: remote)
    r = post("certificado_participante", subject="Tu certificado: Excel")
    assert r.json()["status"] == "sent"
    assert sent[0]["message"]["Subject"] == "Tu certificado: Excel"


def test_smtp_remoto_invalido_usa_env(monkeypatch, sent):
    remote = {
        **REMOTE,
        "smtp": {"host": "x"},
    }
    monkeypatch.setattr(app_module, "get_remote_config", lambda: remote)
    post("certificado_participante", subject="Tu certificado: Excel")
    assert sent[0]["cfg"]["host"] == "smtp.env.local"
