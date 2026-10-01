using Capacitaciones.Application.Dtos.Configuracion;

namespace Capacitaciones.Application.Dtos.Notifications;

/// <summary>Resultado de <c>POST /send-mail</c>: <c>Omitido</c> = el aviso está desactivado en Configuración → Correo.</summary>
public enum MailSendResult
{
    Enviado,
    Omitido
}

/// <summary>Payload de <c>POST /send-test</c> de mail_sender.</summary>
public class SendTestMailRequest
{
    public SmtpInternoDto Smtp { get; set; } = new();
    public string Recipient { get; set; } = string.Empty;
}

/// <summary>Respuesta de <c>POST /send-test</c>. <c>Error</c> trae el mensaje del servidor SMTP.</summary>
public class MailTestResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}
