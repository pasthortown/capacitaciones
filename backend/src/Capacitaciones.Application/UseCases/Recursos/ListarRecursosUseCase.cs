using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>Listado admin del repositorio de recursos.</summary>
public class ListarRecursosUseCase
{
    private readonly IRecursoRepository _repo;

    public ListarRecursosUseCase(IRecursoRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<RecursoListDto>> ExecuteAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = await _repo.ListAsync(includeInactive, ct);
        return items.Select(RecursoMapper.ToList).ToList();
    }
}
