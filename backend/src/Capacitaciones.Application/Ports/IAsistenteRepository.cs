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
    /// Obtiene un asistente a partir de la combinación (capacitación, identificación).
    /// Usado en el flujo público de encuesta para autoidentificar al asistente por cédula.
    /// </summary>
    Task<Asistente?> GetByCapacitacionAndIdentificacionAsync(
        Guid capacitacionId,
        string identificacion,
        CancellationToken ct = default);

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

    /// <summary>
    /// Marca el envío de certificados de una capacitación: los asistentes cuyo id está en
    /// <paramref name="elegibleIds"/> pasan a <see cref="EstadoEnvioCertificado.Pendiente"/>
    /// (limpiando el mensaje de error previo); el resto queda en <c>null</c> (no aplica).
    /// Devuelve la cantidad que quedó pendiente.
    /// </summary>
    Task<int> MarcarEstadoEnvioElegiblesAsync(
        Guid capacitacionId,
        ISet<Guid> elegibleIds,
        CancellationToken ct = default);

    /// <summary>
    /// Reabre los asistentes en estado <see cref="EstadoEnvioCertificado.Error"/> de una
    /// capacitación, volviéndolos a <see cref="EstadoEnvioCertificado.Pendiente"/> y limpiando
    /// el mensaje de error. Devuelve cuántos se reabrieron.
    /// </summary>
    Task<int> MarcarErroresComoPendientesAsync(Guid capacitacionId, CancellationToken ct = default);

    /// <summary>Lista los asistentes de una capacitación que están en el estado de envío dado.</summary>
    Task<IReadOnlyList<Asistente>> ListByEstadoEnvioAsync(
        Guid capacitacionId,
        EstadoEnvioCertificado estado,
        CancellationToken ct = default);

    /// <summary>
    /// Persiste el resultado del envío del certificado de un asistente: estado, timestamp de
    /// envío (solo en éxito) y mensaje de error (solo en fallo). Operación puntual usada por el
    /// worker en segundo plano.
    /// </summary>
    Task ActualizarResultadoEnvioAsync(
        Guid asistenteId,
        EstadoEnvioCertificado estado,
        DateTime? fechaEnvio,
        string? mensajeError,
        CancellationToken ct = default);

    /// <summary>
    /// Devuelve los ids de capacitaciones que tienen al menos un asistente en estado
    /// <see cref="EstadoEnvioCertificado.Pendiente"/>. Lo usa el worker al arrancar para
    /// retomar envíos que quedaron a medias por un reinicio del servidor.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListCapacitacionesConPendientesAsync(CancellationToken ct = default);
}
