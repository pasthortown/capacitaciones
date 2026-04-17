using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>Listado de capacitaciones con filtros opcionales de <c>activo</c> y <c>estado</c>.</summary>
public class ListarCapacitacionesUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly IAsistenteRepository _asistentes;

    public ListarCapacitacionesUseCase(ICapacitacionRepository repo, IAsistenteRepository asistentes)
    {
        _repo = repo;
        _asistentes = asistentes;
    }

    public async Task<IReadOnlyList<CapacitacionListDto>> ExecuteAsync(
        bool includeInactive,
        string? estadoFiltro,
        CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(includeInactive, ct);

        // Conteos batch para evitar N+1 en el dashboard.
        var conteos = await _asistentes.CountByCapacitacionesAsync(items.Select(c => c.Id), ct);

        var dtos = items.Select(c =>
            CapacitacionMapper.ToListDto(c, conteos.TryGetValue(c.Id, out var n) ? n : 0));

        if (!string.IsNullOrWhiteSpace(estadoFiltro))
        {
            // Filtro aplicado en memoria porque Estado es derivado (no persistido).
            dtos = dtos.Where(d =>
                string.Equals(d.Estado, estadoFiltro, StringComparison.OrdinalIgnoreCase));
        }

        return dtos.ToList();
    }
}
