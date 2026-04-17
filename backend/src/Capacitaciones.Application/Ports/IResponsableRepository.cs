using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia del catálogo global de <see cref="Responsable"/>.
/// El admin administra altas/bajas lógicas y ediciones; los UseCases de capacitación
/// consultan existencia/actividad para validar <c>responsableIds</c>; el UseCase público
/// del responsable lee/edita su propio perfil desde el link firmado.
/// </summary>
public interface IResponsableRepository
{
    /// <summary>Listado completo. Si <paramref name="includeInactive"/> es false solo devuelve activos.</summary>
    Task<IReadOnlyList<Responsable>> ListAsync(bool includeInactive, CancellationToken ct = default);

    /// <summary>Obtiene un responsable por id (incluye inactivos).</summary>
    Task<Responsable?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Alta — asume que el caller setea Id/FechaCreacion.</summary>
    Task AddAsync(Responsable entity, CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios del responsable. Si la entidad viene detached, el adaptador la
    /// re-attach para que EF la trackee.
    /// </summary>
    Task UpdateAsync(Responsable entity, CancellationToken ct = default);

    /// <summary>Baja lógica: <c>Activo = false</c> + <c>FechaActualizacion = UtcNow</c>. Idempotente.</summary>
    Task SetInactivoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Devuelve true si existe un responsable con ese id y está activo.</summary>
    Task<bool> ExistsActivoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Devuelve true si TODOS los ids existen y están activos. Útil para validar en bloque
    /// los <c>responsableIds</c> que vienen en el payload de una capacitación.
    /// </summary>
    Task<bool> ExistenActivosAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
