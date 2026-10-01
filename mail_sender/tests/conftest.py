import os
import sys

# Permite `import app` / `import config_provider` corriendo pytest desde mail_sender/.
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

# Variables SMTP mínimas para el fallback a .env en los tests.
os.environ.setdefault("SMTP_HOST", "smtp.env.local")
os.environ.setdefault("SMTP_PORT", "25")
os.environ.setdefault("SMTP_FROM", "env@dos.com.ec")
os.environ.setdefault("TEMPLATES_DIR", os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "plantillas")))
