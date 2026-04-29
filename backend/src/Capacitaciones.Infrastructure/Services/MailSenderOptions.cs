namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Opciones bindeadas desde la sección <c>MailSender</c> de <c>appsettings</c>
/// (env var <c>MailSender__BaseUrl</c>). Apunta al servicio FastAPI
/// <c>mail_sender</c> en la red interna <c>capacitaciones-net</c>.
/// </summary>
public class MailSenderOptions
{
    public const string SectionName = "MailSender";

    public string BaseUrl { get; set; } = "http://mail_sender:8000";

    /// <summary>
    /// Timeout del HttpClient en segundos. El servicio Python usa
    /// <c>smtplib.SMTP(timeout=30)</c>, así que dejamos margen para el handshake
    /// + envío sin bloquear indefinidamente al backend.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
