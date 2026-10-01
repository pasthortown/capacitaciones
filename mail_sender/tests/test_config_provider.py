import pytest

import config_provider


@pytest.fixture(autouse=True)
def _reset():
    config_provider.reset_cache()
    yield
    config_provider.reset_cache()


def test_cachea_dentro_de_la_ventana(monkeypatch):
    llamadas = []
    monkeypatch.setattr(config_provider, "_fetch_remote", lambda: llamadas.append(1) or {"configurado": True})

    assert config_provider.get_remote_config() == {"configurado": True}
    assert config_provider.get_remote_config() == {"configurado": True}
    assert len(llamadas) == 1


def test_refresca_al_vencer(monkeypatch):
    llamadas = []
    monkeypatch.setattr(config_provider, "_fetch_remote", lambda: llamadas.append(1) or {"n": len(llamadas)})
    monkeypatch.setattr(config_provider, "CACHE_SECONDS", 0)

    config_provider.get_remote_config()
    assert config_provider.get_remote_config() == {"n": 2}


def test_backend_caido_sin_cache_devuelve_none(monkeypatch):
    def boom():
        raise OSError("connection refused")

    monkeypatch.setattr(config_provider, "_fetch_remote", boom)
    assert config_provider.get_remote_config() is None


def test_backend_caido_con_cache_vencida_devuelve_la_ultima(monkeypatch):
    monkeypatch.setattr(config_provider, "_fetch_remote", lambda: {"configurado": True})
    monkeypatch.setattr(config_provider, "CACHE_SECONDS", 0)
    config_provider.get_remote_config()

    def boom():
        raise OSError("timeout")

    monkeypatch.setattr(config_provider, "_fetch_remote", boom)
    assert config_provider.get_remote_config() == {"configurado": True}


def test_sin_variables_no_llama_al_backend(monkeypatch):
    monkeypatch.delenv("BACKEND_INTERNAL_URL", raising=False)
    monkeypatch.delenv("MAIL_CONFIG_API_KEY", raising=False)
    assert config_provider._fetch_remote() is None
