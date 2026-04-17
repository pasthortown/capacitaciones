using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: obtener el estado del contador de numeración.</summary>
public class ObtenerNumeracionUseCase
{
    private readonly IConfiguracionNumeracionRepository _repo;

    public ObtenerNumeracionUseCase(IConfiguracionNumeracionRepository repo)
    {
        _repo = repo;
    }

    public async Task<ConfiguracionNumeracionDto> ExecuteAsync(CancellationToken ct = default)
    {
        var cfg = await _repo.GetAsync(ct);
        return new ConfiguracionNumeracionDto
        {
            SiguienteNumero = cfg.SiguienteNumero,
            UltimaActualizacion = cfg.UltimaActualizacion,
            Formato = "CAP-PC-REG-###"
        };
    }
}
