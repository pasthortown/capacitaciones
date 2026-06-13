using System.Globalization;
using Capacitaciones.Application.Common;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Notifications;

/// <summary>
/// Envía al admin que disparó la descarga el reporte de asistencia como PDF
/// adjunto + tarjetas con totales (inscritos / presentes / ausentes), usando
/// la plantilla <c>registro_asistencia_admin</c>.
///
/// El caller (controller) carga el PDF en memoria al servir la descarga y
/// reutiliza esos bytes acá para evitar leer el archivo dos veces.
/// </summary>
public class EnviarReporteAsistenciaAdminUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IMailSenderClient _mail;

    public EnviarReporteAsistenciaAdminUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        IMailSenderClient mail)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _mail = mail;
    }

    public async Task ExecuteAsync(
        Guid capacitacionId,
        byte[] pdfBytes,
        string filename,
        string adminEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminEmail) || pdfBytes is null || pdfBytes.Length == 0)
        {
            return;
        }

        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct);
        if (capacitacion is null)
        {
            return;
        }

        var asistentes = await _asistentes.ListByCapacitacionAsync(capacitacionId, ct);
        var presentes = asistentes.Count(a => a.EstadoAsistencia == EstadoAsistencia.Presente);
        var ausentes = asistentes.Count(a => a.EstadoAsistencia == EstadoAsistencia.Ausente);

        var fechaLocal = EcuadorTime.FromUtc(capacitacion.FechaHoraInicio);
        var fechaTexto = fechaLocal.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-EC"));

        var subject = $"Registro de asistencia: {capacitacion.Tema}";

        var request = new SendMailRequest
        {
            Template = "registro_asistencia_admin",
            Subject = subject,
            Recipients = new List<string> { adminEmail },
            Parameters = new Dictionary<string, object?>
            {
                ["subject"] = subject,
                ["codigo"] = capacitacion.Codigo,
                ["tema"] = capacitacion.Tema,
                ["fecha"] = fechaTexto,
                ["totalInscritos"] = asistentes.Count,
                ["totalPresentes"] = presentes,
                ["totalAusentes"] = ausentes,
            },
            Attachment = new MailAttachment
            {
                Filename = string.IsNullOrWhiteSpace(filename) ? "reporte_asistencia.pdf" : filename,
                ContentBase64 = Convert.ToBase64String(pdfBytes),
                MimeType = "application/pdf",
            }
        };

        await _mail.SendMailAsync(request, ct);
    }
}
