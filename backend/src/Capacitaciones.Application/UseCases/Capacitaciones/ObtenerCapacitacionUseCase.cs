using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>Obtiene una capacitación con todos sus detalles (incluye responsables).</summary>
public class ObtenerCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IAsistenteRepository _asistentes;

    public ObtenerCapacitacionUseCase(ICapacitacionRepository repo, IAsistenteRepository asistentes)
    {
        _repo = repo;
        _asistentes = asistentes;
    }

    public async Task<CapacitacionDetailDto?> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct);
        if (entity is null) return null;
        var total = await _asistentes.CountByCapacitacionAsync(entity.Id, ct);
        return CapacitacionMapper.ToDetailDto(entity, total);
    }
}
