using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Puerto de persistencia para <see cref="Capacitacion"/>. Incluye operaciones
/// específicas para administrar la sub-colección <see cref="Responsable"/>
/// (replace-all en update) y consultar el máximo número de código emitido
/// (usado por la validación del contador de numeración).
/// </summary>
public interface ICapacitacionRepository
{
    /// <summary>Listado para el grid. <paramref name="estadoFiltro"/> es opcional (null = sin filtro).</summary>
    Task<IReadOnlyList<Capacitacion>> ListAsync(
        bool includeInactive = false,
        CancellationToken ct = default);

    /// <summary>Carga una capacitación con su <c>Modalidad</c>, <c>TipoActividad</c> y <c>Responsables</c>.</summary>
    Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Alta transaccional. El repositorio maneja <c>IExecutionStrategy</c> + <c>BeginTransaction</c>
    /// (para SQL Server con retry ante errores transitorios) y dentro de la misma transacción:
    ///   1. Invoca <paramref name="codeFactory"/> para obtener el siguiente código
    ///      (típicamente un wrapper sobre <c>INumeracionService.ClaimNextCodeAsync</c>).
    ///   2. Asigna el código a la entidad y persiste la capacitación + responsables.
    /// Devuelve el código finalmente asignado.
    /// </summary>
    Task<string> AddAsync(
        Capacitacion entity,
        Func<CancellationToken, Task<string>> codeFactory,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza una capacitación reemplazando por completo su lista de responsables
    /// (borra los existentes y agrega los nuevos dentro de una misma transacción).
    /// </summary>
    Task UpdateWithResponsablesAsync(
        Capacitacion entity,
        IEnumerable<Responsable> nuevosResponsables,
        CancellationToken ct = default);

    /// <summary>Eliminación lógica (Activo = false + FechaActualizacion).</summary>
    Task DeleteLogicoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el máximo sufijo numérico emitido en <c>Codigo</c> (parseado de
    /// <c>CAP-PC-REG-###</c>). 0 si no hay capacitaciones registradas. Incluye filas
    /// lógicamente eliminadas — el código es único global y no se reutiliza.
    /// </summary>
    Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default);
}
