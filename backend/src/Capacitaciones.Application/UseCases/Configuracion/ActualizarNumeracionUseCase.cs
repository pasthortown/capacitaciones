using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: actualizar manualmente el contador de numeración.</summary>
public class ActualizarNumeracionUseCase
{
    public const int MinNumero = 1;
    public const int MaxNumero = 999;

    private readonly IConfiguracionNumeracionRepository _repo;

    public ActualizarNumeracionUseCase(IConfiguracionNumeracionRepository repo)
    {
        _repo = repo;
    }

    public async Task<ConfiguracionNumeracionDto> ExecuteAsync(UpdateConfiguracionNumeracionDto input, CancellationToken ct = default)
    {
        if (input is null)
        {
            throw new ConfiguracionNumeracionException("INVALID_INPUT", "Payload requerido.");
        }

        if (input.SiguienteNumero < MinNumero || input.SiguienteNumero > MaxNumero)
        {
            throw new ConfiguracionNumeracionException(
                "OUT_OF_RANGE",
                $"'siguienteNumero' debe estar entre {MinNumero} y {MaxNumero}.");
        }

        // TODO Fase 3: validar siguienteNumero > max(codigoActual) de Capacitaciones ya emitidas.

        var cfg = await _repo.GetAsync(ct);
        cfg.SiguienteNumero = input.SiguienteNumero;
        cfg.UltimaActualizacion = DateTime.UtcNow;
        await _repo.UpdateAsync(cfg, ct);

        return new ConfiguracionNumeracionDto
        {
            SiguienteNumero = cfg.SiguienteNumero,
            UltimaActualizacion = cfg.UltimaActualizacion,
            Formato = "CAP-PC-REG-###"
        };
    }
}

/// <summary>Excepción de validación del caso de uso de configuración de numeración.</summary>
public class ConfiguracionNumeracionException : Exception
{
    public string Codigo { get; }

    public ConfiguracionNumeracionException(string codigo, string message) : base(message)
    {
        Codigo = codigo;
    }
}
