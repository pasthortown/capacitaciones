using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para <see cref="Asistente"/> (inscripción pública Fase 5).
/// Expone operaciones mínimas consumidas por los use cases:
///   - alta al inscribir (con validación previa de duplicado),
///   - listado por capacitación para la vista admin,
///   - verificación de duplicado (capacitación + identificación),
///   - búsqueda por id (stub de certificado).
/// </summary>
public interface IAsistenteRepository
{
    /// <summary>Inserta un nuevo asistente.</summary>
    Task AddAsync(Asistente entity, CancellationToken ct = default);

    /// <summary>Lista de asistentes de una capacitación, con <c>Area</c> cargada para el DTO admin.</summary>
    Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default);

    /// <summary>
    /// <c>true</c> si ya existe un asistente con la misma (capacitación, identificación).
    /// Usado por el caso de uso para dar un error amigable antes de intentar el insert y
    /// así evitar depender del <c>UNIQUE INDEX</c> del motor para el control de duplicados.
    /// </summary>
    Task<bool> ExistsByCapacitacionAndIdentificacionAsync(
        Guid capacitacionId,
        string identificacion,
        CancellationToken ct = default);

    /// <summary>Obtiene un asistente por id, con <c>Capacitacion</c> y <c>Area</c> cargadas.</summary>
    Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios escalares de un <see cref="Asistente"/>. Usado por el pase de lista
    /// (Fase 10) para actualizar <c>EstadoAsistencia</c> / <c>FechaMarcacionAsistencia</c> sin
    /// modificar el resto del registro.
    /// </summary>
    Task UpdateAsync(Asistente entity, CancellationToken ct = default);

    /// <summary>Cuenta asistentes inscritos en una capacitación.</summary>
    Task<int> CountByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default);

    /// <summary>
    /// Cuenta asistentes por capacitación para un conjunto de ids. Devuelve un diccionario
    /// con el total por id (capacitaciones sin asistentes no aparecen — el caller usa 0 como default).
    /// Una sola query agrupada evita N+1 en el listado admin.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByCapacitacionesAsync(
        IEnumerable<Guid> capacitacionIds,
        CancellationToken ct = default);
}
