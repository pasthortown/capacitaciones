using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: leer la configuración SMTP para la pantalla de administración.</summary>
public class ObtenerConfiguracionCorreoUseCase
{
    private readonly IConfiguracionCorreoRepository _repo;
    private readonly ISecretProtector _protector;

    public ObtenerConfiguracionCorreoUseCase(IConfiguracionCorreoRepository repo, ISecretProtector protector)
    {
        _repo = repo;
        _protector = protector;
    }

    public async Task<ConfiguracionCorreoDto> ExecuteAsync(CancellationToken ct = default)
    {
        var cfg = await _repo.GetAsync(ct);
        return ToDto(cfg, _protector);
    }

    internal static ConfiguracionCorreoDto ToDto(ConfiguracionCorreo? cfg, ISecretProtector protector)
    {
        if (cfg is null) return new ConfiguracionCorreoDto { Configurado = false };

        var tienePassword = !string.IsNullOrEmpty(cfg.SmtpPasswordCifrada);
        return new ConfiguracionCorreoDto
        {
            Configurado = true,
            SmtpHost = cfg.SmtpHost,
            SmtpPort = cfg.SmtpPort,
            SmtpUser = cfg.SmtpUser,
            TienePassword = tienePassword,
            PasswordInvalida = tienePassword && protector.TryUnprotect(cfg.SmtpPasswordCifrada!) is null,
            UsarTls = cfg.UsarTls,
            RemitenteCorreo = cfg.RemitenteCorreo,
            RemitenteNombre = cfg.RemitenteNombre,
            CcGlobal = cfg.CcGlobal,
            BccGlobal = cfg.BccGlobal,
            ActualizadoPor = cfg.ActualizadoPor,
            ActualizadoEn = cfg.ActualizadoEn
        };
    }
}
