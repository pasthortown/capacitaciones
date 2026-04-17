using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>Obtiene una capacitación con todos sus detalles (incluye responsables).</summary>
public class ObtenerCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;

    public ObtenerCapacitacionUseCase(ICapacitacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<CapacitacionDetailDto?> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct);
        return entity is null ? null : CapacitacionMapper.ToDetailDto(entity);
    }
}
