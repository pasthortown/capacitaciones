import os
import smtplib
from email.mime.text import MIMEText


def send_mail(to_address: str, subject: str, body: str) -> None:
    smtp_host = os.environ["SMTP_HOST"]
    smtp_port = int(os.environ["SMTP_PORT"])
    sender = os.environ["SMTP_FROM"]
    password = os.environ["SMTP_PASSWORD"]
    use_tls = os.environ.get("SMTP_USE_TLS", "true").lower() == "true"

    msg = MIMEText(body, "plain", "utf-8")
    msg["Subject"] = subject
    msg["From"] = sender
    msg["To"] = to_address

    with smtplib.SMTP(smtp_host, smtp_port) as server:
        server.ehlo()
        if use_tls:
            server.starttls()
            server.ehlo()
        server.login(sender, password)
        server.sendmail(sender, [to_address], msg.as_string())


if __name__ == "__main__":
    send_mail(
        to_address="lasalazar@dos.com.ec",
        subject="Saludo",
        body="hola Luis",
    )
    print("Correo enviado a lasalazar@dos.com.ec")
