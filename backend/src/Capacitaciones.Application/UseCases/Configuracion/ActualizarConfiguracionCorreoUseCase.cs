using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: guardar la configuración SMTP / remitente / copias globales.</summary>
public class ActualizarConfiguracionCorreoUseCase
{
    private readonly IConfiguracionCorreoRepository _repo;
    private readonly ISecretProtector _protector;

    public ActualizarConfiguracionCorreoUseCase(IConfiguracionCorreoRepository repo, ISecretProtector protector)
    {
        _repo = repo;
        _protector = protector;
    }

    public async Task<ConfiguracionCorreoDto> ExecuteAsync(
        UpdateConfiguracionCorreoDto input, string adminEmail, CancellationToken ct = default)
    {
        if (input is null) throw new ConfiguracionCorreoException("INVALID_INPUT", "Payload requerido.");

        var errores = ConfiguracionCorreoValidator.Validar(input);
        if (errores.Count > 0)
        {
            throw new ConfiguracionCorreoException("VALIDACION", "Revisa los campos marcados.", errores);
        }

        var existente = await _repo.GetAsync(ct);
        ConfiguracionCorreoValidator.ExigirPasswordSiCambiaServidor(input, existente);

        var passwordNueva = string.IsNullOrEmpty(input.Password) ? null : input.Password;
        if (passwordNueva is not null && !input.QuitarPassword && !_protector.IsConfigured)
        {
            throw new ConfiguracionCorreoException(
                "ENCRYPTION_KEY_MISSING",
                "El servidor no tiene configurada la llave de cifrado (CORREO_ENCRYPTION_KEY). Contacta al administrador del sistema.");
        }

        var cfg = existente ?? new ConfiguracionCorreo { Id = 1 };
        cfg.SmtpHost = input.SmtpHost.Trim();
        cfg.SmtpPort = input.SmtpPort;
        cfg.SmtpUser = string.IsNullOrWhiteSpace(input.SmtpUser) ? null : input.SmtpUser.Trim();
        cfg.UsarTls = input.UsarTls;
        cfg.RemitenteCorreo = input.RemitenteCorreo.Trim();
        cfg.RemitenteNombre = string.IsNullOrWhiteSpace(input.RemitenteNombre) ? null : input.RemitenteNombre.Trim();
        cfg.CcGlobal = ConfiguracionCorreoValidator.Normalizar(input.CcGlobal);
        cfg.BccGlobal = ConfiguracionCorreoValidator.Normalizar(input.BccGlobal);

        if (input.QuitarPassword)
        {
            cfg.SmtpPasswordCifrada = null;
        }
        else if (passwordNueva is not null)
        {
            cfg.SmtpPasswordCifrada = _protector.Protect(passwordNueva);
        }

        cfg.ActualizadoPor = adminEmail;
        cfg.ActualizadoEn = DateTime.UtcNow;
        await _repo.UpsertAsync(cfg, ct);

        return ObtenerConfiguracionCorreoUseCase.ToDto(cfg, _protector);
    }
}
