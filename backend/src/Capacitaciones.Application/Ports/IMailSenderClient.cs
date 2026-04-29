using Capacitaciones.Application.Dtos.Notifications;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto hexagonal que abstrae al servicio externo <c>mail_sender</c> (FastAPI,
/// HTTP interno en <c>http://mail_sender:8000</c>). El adapter de Infrastructure
/// implementa el envío real vía <c>HttpClient</c>; los tests inyectan un fake.
/// </summary>
public interface IMailSenderClient
{
    /// <summary>
    /// Invoca <c>POST /send-mail</c>. Lanza <see cref="HttpRequestException"/>
    /// si el servicio no responde o devuelve un código no exitoso. El caller
    /// decide si propaga el error o lo silencia (el flujo de notificaciones
    /// del backend lo silencia para no bloquear la operación principal).
    /// </summary>
    Task SendMailAsync(SendMailRequest request, CancellationToken ct);
}
