using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>Listado admin del catálogo de responsables.</summary>
public class ListarResponsablesUseCase
{
    private readonly IResponsableRepository _repo;

    public ListarResponsablesUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<ResponsableSummaryDto>> ExecuteAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(includeInactive, ct);
        return items.Select(ResponsableMapper.ToSummary).ToList();
    }
}
