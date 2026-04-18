using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Caso de uso admin: baja lógica del recurso + borrado físico del archivo.
/// Idempotente: si el archivo ya no existe, el storage lo trata como no-op.
/// Mantener la fila con <c>Activo=false</c> preserva trazabilidad histórica; el índice
/// único en <c>NombreAlmacenado</c> sigue siendo consistente porque cada alta usa un GUID nuevo.
/// </summary>
public class EliminarRecursoUseCase
{
    private readonly IRecursoRepository _repo;
    private readonly IResourceStorage _storage;

    public EliminarRecursoUseCase(IRecursoRepository repo, IResourceStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct) ?? throw new RecursoNotFoundException(id);

        // Primero la baja lógica en BD; si falla, preferimos conservar el archivo.
        await _repo.DeleteAsync(entity.Id, ct);

        // Luego borramos el archivo físico (no-op si ya no existe).
        await _storage.DeleteAsync(entity.NombreAlmacenado, ct);
    }
}
