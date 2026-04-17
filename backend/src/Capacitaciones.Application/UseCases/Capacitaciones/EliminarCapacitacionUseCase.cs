using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>Caso de uso: eliminar capacitación (borrado lógico: Activo = false).</summary>
public class EliminarCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;

    public EliminarCapacitacionUseCase(ICapacitacionRepository repo)
    {
        _repo = repo;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct)
            ?? throw new CapacitacionNotFoundException(id);

        await _repo.DeleteLogicoAsync(entity.Id, ct);
    }
}
