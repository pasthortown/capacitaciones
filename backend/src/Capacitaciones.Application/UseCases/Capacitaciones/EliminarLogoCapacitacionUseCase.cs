using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Fase 9 — Caso de uso admin: elimina el logo de una capacitación (archivo físico + columnas).
/// Idempotente: si la capacitación no tiene logo, no hace nada y devuelve sin error.
/// </summary>
public class EliminarLogoCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;
    private readonly ILogoCapacitacionStorage _storage;

    public EliminarLogoCapacitacionUseCase(ICapacitacionRepository repo, ILogoCapacitacionStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdWithResponsablesAsync(id, ct)
            ?? throw new CapacitacionNotFoundException(id);

        var logoPath = entity.LogoPath;
        if (string.IsNullOrWhiteSpace(logoPath))
        {
            // Nada que borrar. Mantenemos idempotencia: el endpoint devuelve 204 igualmente.
            return;
        }

        entity.LogoPath = null;
        entity.LogoContentType = null;
        entity.FechaActualizacion = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);

        // Best-effort: si falla el borrado físico queda archivo huérfano pero BD ya está limpia.
        try { await _storage.EliminarAsync(logoPath!, ct); }
        catch { /* swallow: el adaptador loggea si corresponde */ }
    }
}
