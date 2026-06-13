using System.Globalization;
using Capacitaciones.Application.Common;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Notifications;

/// <summary>
/// Contexto del envío de la invitación. Determina el subject del correo:
/// el cuerpo (datos + link + QR) es el mismo en los tres casos.
/// </summary>
public enum InvitacionContexto
{
    /// <summary>Click manual del admin sobre "Enviar email de inscripción".</summary>
    Manual,
    /// <summary>Disparo automático tras crear una capacitación.</summary>
    CapacitacionCreada,
    /// <summary>Disparo automático tras editar una capacitación.</summary>
    CapacitacionActualizada,
}

/// <summary>
/// Genera el correo "invitación a inscribirse" — pensado para que el admin
/// lo reciba y lo reenvíe (forward) a los interesados. Lleva el tema, datos
/// del evento, link público de inscripción y QR. Usa la plantilla
/// <c>invitacion_inscripcion</c> de mail_sender.
///
/// El recipient es el admin que dispara la acción (el que tiene la sesión).
/// La idea: él lo abre desde su buzón y lo reenvía con un click.
/// </summary>
public class EnviarInvitacionInscripcionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IQrGenerator _qr;
    private readonly IMailSenderClient _mail;
    private readonly INotificationsConfig _config;

    public EnviarInvitacionInscripcionUseCase(
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

    public Task<InvitacionInscripcionResultDto> ExecuteAsync(Guid capacitacionId, string adminEmail, CancellationToken ct = default)
        => ExecuteAsync(capacitacionId, adminEmail, InvitacionContexto.Manual, ct);

    public async Task<InvitacionInscripcionResultDto> ExecuteAsync(
        Guid capacitacionId,
        string adminEmail,
        InvitacionContexto contexto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            throw new CapacitacionServiceException(
                "ADMIN_EMAIL_REQUERIDO",
                "No se identificó el email del administrador autenticado.");
        }

        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var token = _jwt.GenerateInscripcionToken(entity.Id);
        var publicBase = (_config.PublicBaseUrl ?? string.Empty).TrimEnd('/');
        var inscripcionUrl = $"{publicBase}/inscripcion?token={Uri.EscapeDataString(token.Token)}";
        var qrBase64 = _qr.GeneratePngBase64(inscripcionUrl);

        var tipoActividad = entity.TipoActividad?.Nombre ?? "evento";
        var modalidad = entity.Modalidad?.Nombre ?? string.Empty;

        var cultura = new CultureInfo("es-EC");
        var fechaLocal = EcuadorTime.FromUtc(entity.FechaHoraInicio);

        var horas = entity.DuracionMinutos / 60;
        var minutosRestantes = entity.DuracionMinutos % 60;
        var duracionTexto = minutosRestantes == 0 ? $"{horas} h" : $"{horas} h {minutosRestantes} min";

        var subject = contexto switch
        {
            InvitacionContexto.CapacitacionCreada => $"{tipoActividad} Creado",
            InvitacionContexto.CapacitacionActualizada => $"{tipoActividad} Actualizado",
            _ => $"Invitación a {tipoActividad}: {entity.Tema}",
        };

        var request = new SendMailRequest
        {
            Template = "invitacion_inscripcion",
            Subject = subject,
            Recipients = new List<string> { adminEmail },
            Parameters = new Dictionary<string, object?>
            {
                ["subject"] = subject,
                ["tema"] = entity.Tema,
                ["capacitador"] = entity.Capacitador,
                ["tipoActividad"] = tipoActividad,
                ["modalidad"] = modalidad,
                ["fecha"] = fechaLocal.ToString("dd 'de' MMMM 'de' yyyy", cultura),
                ["hora"] = fechaLocal.ToString("HH:mm", cultura),
                ["duracion"] = duracionTexto,
                ["tipoCertificacion"] = entity.TipoCertificacion == TipoCertificacion.Aprobacion ? "Aprobación" : "Participación",
                ["linkInscripcion"] = inscripcionUrl,
                ["qrBase64"] = qrBase64
            }
        };

        await _mail.SendMailAsync(request, ct);

        return new InvitacionInscripcionResultDto
        {
            Recipient = adminEmail,
            LinkInscripcion = inscripcionUrl
        };
    }
}

/// <summary>Resultado del envío de la invitación.</summary>
public class InvitacionInscripcionResultDto
{
    public string Recipient { get; set; } = string.Empty;
    public string LinkInscripcion { get; set; } = string.Empty;
}
