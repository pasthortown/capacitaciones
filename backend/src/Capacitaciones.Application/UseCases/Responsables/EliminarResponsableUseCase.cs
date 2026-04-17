using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Caso de uso admin: baja lógica de un responsable (<c>Activo = false</c>). Idempotente.
/// Capacitaciones que ya referencian al responsable mantienen la relación — solo se impide
/// seleccionarlo en nuevas capacitaciones.
/// </summary>
public class EliminarResponsableUseCase
{
    private readonly IResponsableRepository _repo;

    public EliminarResponsableUseCase(IResponsableRepository repo)
    {
        _repo = repo;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new ResponsableNotFoundException(id);
        await _repo.SetInactivoAsync(entity.Id, ct);
    }
}
