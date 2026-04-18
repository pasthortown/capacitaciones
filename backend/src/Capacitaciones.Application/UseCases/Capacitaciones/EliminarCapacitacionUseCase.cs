using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Caso de uso: eliminar capacitación (borrado lógico: Activo = false).
/// Fase 9: si la capacitación tiene logo asociado, se borra el archivo físico del volumen
/// (best-effort) antes del soft-delete, para no dejar binarios huérfanos en el storage.
/// </summary>
public class EliminarCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly ILogoCapacitacionStorage _logoStorage;

    public EliminarCapacitacionUseCase(ICapacitacionRepository repo, ILogoCapacitacionStorage logoStorage)
    {
        _repo = repo;
        _logoStorage = logoStorage;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct)
            ?? throw new CapacitacionNotFoundException(id);

        // Primero el soft-delete. Si falla, no tocamos el archivo físico.
        await _repo.DeleteLogicoAsync(entity.Id, ct);

        // Luego limpieza del archivo físico (idempotente, best-effort).
        if (!string.IsNullOrWhiteSpace(entity.LogoPath))
        {
            try { await _logoStorage.EliminarAsync(entity.LogoPath!, ct); }
            catch { /* swallow: el adaptador loggea si corresponde */ }
        }
    }
}
