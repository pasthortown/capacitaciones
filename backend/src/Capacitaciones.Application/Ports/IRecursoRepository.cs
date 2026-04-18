using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia del módulo Repositorio. El caso de uso de subida ya coordina
/// storage físico + metadata; este puerto solo maneja la tabla. La eliminación es soft
/// (<c>Activo=false</c>) pero el UseCase sí borra el archivo físico por el adaptador
/// <see cref="IResourceStorage"/>.
/// </summary>
public interface IRecursoRepository
{
    /// <summary>Alta de un recurso. El caller asigna Id y FechaCreacion.</summary>
    Task AddAsync(Recurso entity, CancellationToken ct = default);

    /// <summary>Listado ordenado por FechaCreacion DESC. Si <paramref name="includeInactive"/> es false solo retorna activos.</summary>
    Task<IReadOnlyList<Recurso>> ListAsync(bool includeInactive, CancellationToken ct = default);

    /// <summary>Obtiene un recurso por Id (incluye inactivos).</summary>
    Task<Recurso?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persiste cambios sobre la entidad (re-attach si viene detached).</summary>
    Task UpdateAsync(Recurso entity, CancellationToken ct = default);

    /// <summary>
    /// Baja lógica: <c>Activo = false</c> + <c>FechaActualizacion = UtcNow</c>. Idempotente.
    /// No elimina la fila (el caller es responsable de borrar el archivo físico).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
