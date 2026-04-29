using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Responsables;

namespace Capacitaciones.Application.UseCases.Notifications;

/// <summary>
/// Envía al responsable (a su <c>Email</c>) el correo con la plantilla
/// <c>responsable_firma</c>: link firmado + QR para que cargue su nombre,
/// cargo, empresa y firma.
///
/// Lo invoca <c>ResponsablesController</c> tras crear o editar un responsable
/// (fire-and-forget, no debe revertir la operación CRUD si el correo falla).
/// </summary>
public class NotificarResponsableFirmaUseCase
{
    private readonly IResponsableRepository _repo;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IQrGenerator _qr;
    private readonly IMailSenderClient _mail;
    private readonly INotificationsConfig _config;

    public NotificarResponsableFirmaUseCase(
        IResponsableRepository repo,
        IJwtTokenGenerator jwt,
        IQrGenerator qr,
        IMailSenderClient mail,
        INotificationsConfig config)
    {
        _repo = repo;
        _jwt = jwt;
        _qr = qr;
        _mail = mail;
        _config = config;
    }

    public async Task ExecuteAsync(Guid responsableId, CancellationToken ct = default)
    {
        var responsable = await _repo.GetByIdAsync(responsableId, ct);
        if (responsable is null || !responsable.Activo)
        {
            return;
        }

        var email = (responsable.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            // Sin email no hay correo posible; salimos en silencio (no es error).
            return;
        }

        var token = _jwt.GenerateResponsableToken(responsable.Id);
        var publicBase = (_config.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var url = $"{publicBase}/responsable?token={Uri.EscapeDataString(token.Token)}";
        var qrBase64 = _qr.GeneratePngBase64(url);

        var subject = $"Carga tus datos y firma en CapacitaDOS";
        var request = new SendMailRequest
        {
            Template = "responsable_firma",
            Subject = subject,
            Recipients = new List<string> { email },
            Parameters = new Dictionary<string, object?>
            {
                ["subject"] = subject,
                ["nombre"] = responsable.Nombres,
                ["link"] = url,
                ["qrBase64"] = qrBase64
            }
        };

        await _mail.SendMailAsync(request, ct);
    }
}
