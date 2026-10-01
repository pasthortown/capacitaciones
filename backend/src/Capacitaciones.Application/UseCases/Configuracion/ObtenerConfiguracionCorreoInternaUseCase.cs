using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>
/// Caso de uso: configuración completa (con contraseña en claro) para mail_sender.
/// Solo se expone por el endpoint interno protegido con X-Internal-Key.
/// </summary>
public class ObtenerConfiguracionCorreoInternaUseCase
{
    private readonly IConfiguracionCorreoRepository _correo;
    private readonly IConfiguracionNotificacionRepository _notificaciones;
    private readonly ISecretProtector _protector;

    public ObtenerConfiguracionCorreoInternaUseCase(
        IConfiguracionCorreoRepository correo,
        IConfiguracionNotificacionRepository notificaciones,
        ISecretProtector protector)
    {
        _correo = correo;
        _notificaciones = notificaciones;
        _protector = protector;
    }

    public async Task<CorreoConfigInternaDto> ExecuteAsync(CancellationToken ct = default)
    {
        var cfg = await _correo.GetAsync(ct);
        var reglas = await _notificaciones.ListAsync(ct);

        var dto = new CorreoConfigInternaDto
        {
            Notificaciones = reglas.ToDictionary(
                r => r.Plantilla,
                r => new NotificacionReglaDto { Activo = r.Activo, Asunto = r.AsuntoPersonalizado })
        };

        if (cfg is null) return dto;

        string? password = null;
        if (!string.IsNullOrEmpty(cfg.SmtpPasswordCifrada))
        {
            password = _protector.TryUnprotect(cfg.SmtpPasswordCifrada);
            dto.PasswordInvalida = password is null;
        }

        dto.Configurado = true;
        dto.Smtp = new SmtpInternoDto
        {
            Host = cfg.SmtpHost,
            Port = cfg.SmtpPort,
            User = cfg.SmtpUser,
            Password = password,
            UseTls = cfg.UsarTls,
            From = cfg.RemitenteCorreo,
            FromName = cfg.RemitenteNombre
        };
        dto.CcGlobal = ConfiguracionCorreoValidator.ParsearLista(cfg.CcGlobal);
        dto.BccGlobal = ConfiguracionCorreoValidator.ParsearLista(cfg.BccGlobal);
        return dto;
    }
}
