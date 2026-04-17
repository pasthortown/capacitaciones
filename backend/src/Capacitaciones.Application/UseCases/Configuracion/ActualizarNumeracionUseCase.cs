using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: actualizar manualmente el contador de numeración.</summary>
public class ActualizarNumeracionUseCase
{
    public const int MinNumero = 1;
    public const int MaxNumero = 999;

    private readonly IConfiguracionNumeracionRepository _repo;
    private readonly ICapacitacionRepository _capacitacionRepo;

    public ActualizarNumeracionUseCase(
        IConfiguracionNumeracionRepository repo,
        ICapacitacionRepository capacitacionRepo)
    {
        _repo = repo;
        _capacitacionRepo = capacitacionRepo;
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

        // Validación Fase 3: el próximo número debe ser mayor que el máximo ya emitido
        // (parseado del sufijo de Capacitacion.Codigo). Incluye filas inactivas.
        var maxEmitido = await _capacitacionRepo.GetMaxCodigoNumberAsync(ct);
        if (maxEmitido > 0 && input.SiguienteNumero <= maxEmitido)
        {
            throw new ConfiguracionNumeracionException(
                "BELOW_MAX_EMITTED",
                $"El siguiente número debe ser mayor a {maxEmitido} (último código emitido: CAP-PC-REG-{maxEmitido:D3}).");
        }

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
