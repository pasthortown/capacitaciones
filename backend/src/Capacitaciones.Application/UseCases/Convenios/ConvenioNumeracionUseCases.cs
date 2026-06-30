using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Obtiene el estado del contador de numeración de convenios.</summary>
public class ObtenerConvenioNumeracionUseCase
{
    private readonly IConvenioNumeracionRepository _repo;

    public ObtenerConvenioNumeracionUseCase(IConvenioNumeracionRepository repo) => _repo = repo;

    public async Task<ConvenioNumeracionDto> ExecuteAsync(CancellationToken ct = default)
    {
        var cfg = await _repo.GetAsync(ct);
        return new ConvenioNumeracionDto
        {
            SiguienteNumero = cfg.SiguienteNumero,
            UltimaActualizacion = cfg.UltimaActualizacion,
            SiguienteCodigo = IConvenioNumeracionService.Format(cfg.SiguienteNumero),
        };
    }
}

/// <summary>Actualiza manualmente el contador de numeración de convenios.</summary>
public class ActualizarConvenioNumeracionUseCase
{
    public const int MinNumero = 1;

    private readonly IConvenioNumeracionRepository _repo;
    private readonly IConvenioRepository _convenios;

    public ActualizarConvenioNumeracionUseCase(IConvenioNumeracionRepository repo, IConvenioRepository convenios)
    {
        _repo = repo;
        _convenios = convenios;
    }

    public async Task<ConvenioNumeracionDto> ExecuteAsync(UpdateConvenioNumeracionDto input, CancellationToken ct = default)
    {
        if (input is null)
            throw new ConvenioValidacionException("Payload requerido.");

        if (input.SiguienteNumero < MinNumero)
            throw new ConvenioValidacionException($"'siguienteNumero' debe ser ≥ {MinNumero}.");

        // El próximo número debe ser mayor al máximo ya emitido (incluye inactivos).
        var maxEmitido = await _convenios.GetMaxNumeroRegistroAsync(ct);
        if (maxEmitido > 0 && input.SiguienteNumero <= maxEmitido)
        {
            throw new ConvenioValidacionException(
                $"El siguiente número debe ser mayor a {maxEmitido} (último código emitido: {IConvenioNumeracionService.Format(maxEmitido)}).");
        }

        var cfg = await _repo.GetAsync(ct);
        cfg.SiguienteNumero = input.SiguienteNumero;
        cfg.UltimaActualizacion = DateTime.UtcNow;
        await _repo.UpdateAsync(cfg, ct);

        return new ConvenioNumeracionDto
        {
            SiguienteNumero = cfg.SiguienteNumero,
            UltimaActualizacion = cfg.UltimaActualizacion,
            SiguienteCodigo = IConvenioNumeracionService.Format(cfg.SiguienteNumero),
        };
    }
}
