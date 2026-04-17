using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>Listado de capacitaciones con filtros opcionales de <c>activo</c> y <c>estado</c>.</summary>
public class ListarCapacitacionesUseCase
{
    private readonly ICapacitacionRepository _repo;

    public ListarCapacitacionesUseCase(ICapacitacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<CapacitacionListDto>> ExecuteAsync(
        bool includeInactive,
        string? estadoFiltro,
        CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(includeInactive, ct);
        var dtos = items.Select(CapacitacionMapper.ToListDto);

        if (!string.IsNullOrWhiteSpace(estadoFiltro))
        {
            // Filtro aplicado en memoria porque Estado es derivado (no persistido).
            dtos = dtos.Where(d =>
                string.Equals(d.Estado, estadoFiltro, StringComparison.OrdinalIgnoreCase));
        }

        return dtos.ToList();
    }
}
