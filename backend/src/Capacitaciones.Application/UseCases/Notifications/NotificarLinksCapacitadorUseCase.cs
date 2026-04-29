using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Notifications;

/// <summary>
/// Envía al capacitador (a su <c>EmailCapacitador</c>) los dos correos con
/// plantillas + QR que le habilitan los flujos del link firmado:
/// <list type="bullet">
///   <item><c>capacitador_descripcion</c> — link/QR del formulario para cargar descripción y firma.</item>
///   <item><c>capacitador_pase_lista</c> — link/QR del flujo de pase de lista.</item>
/// </list>
///
/// Lo invoca el controller (a) automáticamente tras crear una capacitación
/// y (b) bajo demanda desde el botón "Enviar correos para capacitador" del
/// dashboard. La operación es atómica desde la perspectiva del front: si el
/// segundo correo falla, se reporta el error global; el caller decide si
/// reintenta. Los errores de red/SMTP suben como excepciones.
/// </summary>
public class NotificarLinksCapacitadorUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IQrGenerator _qr;
    private readonly IMailSenderClient _mail;
    private readonly INotificationsConfig _config;

    public NotificarLinksCapacitadorUseCase(
        ICapacitacionRepository repo,
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

    public async Task<NotificacionCapacitadorResultDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var email = (entity.EmailCapacitador ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new CapacitacionServiceException(
                "EMAIL_CAPACITADOR_REQUERIDO",
                "La capacitación no tiene email del capacitador registrado; agrega uno antes de enviar los correos.");
        }

        var publicBase = (_config.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var nombreCapacitador = entity.Capacitador;
        var tema = entity.Tema;

        // Token + URL absoluta para descripción/firma.
        var descripcionToken = _jwt.GenerateCapacitadorToken(entity.Id);
        var descripcionUrl = $"{publicBase}/capacitador?token={Uri.EscapeDataString(descripcionToken.Token)}";
        var descripcionQr = _qr.GeneratePngBase64(descripcionUrl);

        var subjectDescripcion = $"Cargar información del curso: {tema}";
        var requestDescripcion = new SendMailRequest
        {
            Template = "capacitador_descripcion",
            Subject = subjectDescripcion,
            Recipients = new List<string> { email },
            Parameters = new Dictionary<string, object?>
            {
                ["subject"] = subjectDescripcion,
                ["nombre"] = nombreCapacitador,
                ["tema"] = tema,
                ["link"] = descripcionUrl,
                ["qrBase64"] = descripcionQr
            }
        };

        // Token + URL absoluta para pase de lista.
        var paseListaToken = _jwt.GeneratePaseListaToken(entity.Id);
        var paseListaUrl = $"{publicBase}/capacitador/pase-lista?token={Uri.EscapeDataString(paseListaToken.Token)}";
        var paseListaQr = _qr.GeneratePngBase64(paseListaUrl);

        var subjectPaseLista = $"Pase de lista: {tema}";
        var requestPaseLista = new SendMailRequest
        {
            Template = "capacitador_pase_lista",
            Subject = subjectPaseLista,
            Recipients = new List<string> { email },
            Parameters = new Dictionary<string, object?>
            {
                ["subject"] = subjectPaseLista,
                ["nombre"] = nombreCapacitador,
                ["tema"] = tema,
                ["link"] = paseListaUrl,
                ["qrBase64"] = paseListaQr
            }
        };

        await _mail.SendMailAsync(requestDescripcion, ct);
        await _mail.SendMailAsync(requestPaseLista, ct);

        return new NotificacionCapacitadorResultDto
        {
            Recipient = email,
            Templates = new List<string> { "capacitador_descripcion", "capacitador_pase_lista" }
        };
    }
}

/// <summary>Respuesta del envío masivo de correos al capacitador.</summary>
public class NotificacionCapacitadorResultDto
{
    public string Recipient { get; set; } = string.Empty;
    public List<string> Templates { get; set; } = new();
}
