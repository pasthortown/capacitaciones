using System.Globalization;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Notifications;

/// <summary>
/// Envía al administrador que dispara la creación o edición de una capacitación
/// un correo con el resumen del evento, el QR y el link de inscripción pública
/// (plantilla <c>resumen_evento_admin</c> de <c>mail_sender</c>).
///
/// Es no-bloqueante respecto a la operación principal: el caller (controller)
/// invoca esto dentro de un <c>try/catch</c> y nunca debe revertir el create/update
/// porque el correo haya fallado. Por eso devuelve <c>void</c> (vía <see cref="Task"/>)
/// y propaga las excepciones — quien decide silenciarlas es el caller.
/// </summary>
public class NotificarResumenCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IQrGenerator _qr;
    private readonly IMailSenderClient _mail;
    private readonly INotificationsConfig _config;

    public NotificarResumenCapacitacionUseCase(
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

    public async Task ExecuteAsync(Guid capacitacionId, string adminEmail, bool isCreate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            // Sin destinatario no hay correo que enviar.
            return;
        }

        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct);
        if (entity is null)
        {
            return;
        }

        // Token + URL absoluta de inscripción pública (lo que va al QR y al body del correo).
        var token = _jwt.GenerateInscripcionToken(entity.Id);
        var publicBase = (_config.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var inscripcionUrl = $"{publicBase}/inscripcion?token={Uri.EscapeDataString(token.Token)}";

        var qrBase64 = _qr.GeneratePngBase64(inscripcionUrl);

        var tipoActividad = entity.TipoActividad?.Nombre ?? "Capacitación";
        var modalidad = entity.Modalidad?.Nombre ?? string.Empty;

        // Convertimos UTC del DB a hora local del proyecto (America/Guayaquil) para el correo.
        var cultura = new CultureInfo("es-EC");
        var fechaLocal = entity.FechaHoraInicio.Kind == DateTimeKind.Utc
            ? entity.FechaHoraInicio.ToLocalTime()
            : entity.FechaHoraInicio;

        var horas = entity.DuracionMinutos / 60;
        var minutosRestantes = entity.DuracionMinutos % 60;
        var duracionTexto = minutosRestantes == 0 ? $"{horas} h" : $"{horas} h {minutosRestantes} min";

        var subject = isCreate
            ? $"{tipoActividad} Creado"
            : $"{tipoActividad} Actualizado";

        var request = new SendMailRequest
        {
            Template = "resumen_evento_admin",
            Subject = subject,
            Recipients = new List<string> { adminEmail },
            Parameters = new Dictionary<string, object?>
            {
                ["subject"] = subject,
                ["codigo"] = entity.Codigo,
                ["tema"] = entity.Tema,
                ["capacitador"] = entity.Capacitador,
                ["tipoActividad"] = tipoActividad,
                ["modalidad"] = modalidad,
                ["fecha"] = fechaLocal.ToString("dd 'de' MMMM 'de' yyyy", cultura),
                ["hora"] = fechaLocal.ToString("HH:mm", cultura),
                ["duracion"] = duracionTexto,
                ["linkInscripcion"] = inscripcionUrl,
                ["qrBase64"] = qrBase64
            }
        };

        await _mail.SendMailAsync(request, ct);
    }
}

/// <summary>
/// Configuración inyectada que expone la URL pública del SPA. La implementación
/// concreta vive en Infrastructure y se alimenta del binding <c>Notifications:PublicBaseUrl</c>
/// (<c>Notifications__PublicBaseUrl</c> como env var).
/// </summary>
public interface INotificationsConfig
{
    string PublicBaseUrl { get; }
}
