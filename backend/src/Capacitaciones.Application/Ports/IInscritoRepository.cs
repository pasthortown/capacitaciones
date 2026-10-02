using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Fila de la pantalla admin "Inscritos": proyección de <see cref="Asistente"/> sin la firma (que
/// puede pesar cientos de KB) — solo <see cref="TieneFirma"/>.
/// </summary>
public record InscritoResumen(
    Guid Id,
    Guid CapacitacionId,
    string CapacitacionCodigo,
    string CapacitacionTema,
    string Nombres,
    string Apellidos,
    string Identificacion,
    Guid AreaId,
    string AreaNombre,
    string EmailUsuario,
    DateTime FechaInscripcion,
    bool TieneFirma);

/// <summary>
/// Puerto de la pantalla admin "Inscritos" (consulta transversal a todas las capacitaciones y
/// edición de un inscrito).
///
/// <see cref="GetForEditAsync"/> devuelve la entidad <i>tracked</i>; los cambios hechos sobre ella y
/// sobre la <see cref="PersonaInscrita"/> pendiente en <see cref="IPersonaInscritaRepository"/> se
/// guardan juntos en <see cref="SaveChangesAsync"/> (mismo DbContext por scope).
/// </summary>
public interface IInscritoRepository
{
    /// <summary>
    /// Lista inscritos de capacitaciones activas, opcionalmente de una sola capacitación y filtrando
    /// por cédula, nombres, apellidos o correo (contiene). Orden: fecha de inscripción descendente.
    /// </summary>
    Task<IReadOnlyList<InscritoResumen>> ListAsync(Guid? capacitacionId, string? buscar, CancellationToken ct = default);

    /// <summary>Asistente tracked con <c>Area</c> y <c>Capacitacion</c> cargadas, o null.</summary>
    Task<Asistente?> GetForEditAsync(Guid id, CancellationToken ct = default);

    /// <summary>true si otro asistente (distinto de <paramref name="excluirId"/>) de la misma capacitación usa la identificación.</summary>
    Task<bool> ExistsOtroConIdentificacionAsync(Guid capacitacionId, string identificacion, Guid excluirId, CancellationToken ct = default);

    /// <summary>
    /// true si existe otra inscripción (distinta de <paramref name="excluirId"/>) de la misma identificación
    /// con fecha posterior a <paramref name="fechaInscripcion"/>. El registro de personas refleja la más reciente.
    /// </summary>
    Task<bool> ExistsInscripcionMasRecienteAsync(string identificacion, DateTime fechaInscripcion, Guid excluirId, CancellationToken ct = default);

    /// <summary>Persiste los cambios pendientes. Traduce el choque del índice único de asistentes a <c>InscripcionDuplicadaException</c>.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
