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
    /// Actualiza una capacitación reemplazando por completo sus relaciones con responsables
    /// (borra las entradas pivote existentes y agrega las nuevas dentro de una misma transacción).
    /// El catálogo global de <see cref="Responsable"/> NO se toca — solo la pivote.
    /// </summary>
    Task UpdateWithResponsablesAsync(
        Capacitacion entity,
        IEnumerable<CapacitacionResponsable> nuevasRelaciones,
        CancellationToken ct = default);

    /// <summary>
    /// Persiste los cambios escalares de una <see cref="Capacitacion"/> ya materializada
    /// (tracked o detached) sin tocar su lista de <see cref="Responsable"/>. Usado por el
    /// caso de uso del capacitador (Fase 4), que solo edita campos propios de la entidad.
    /// </summary>
    Task UpdateAsync(Capacitacion entity, CancellationToken ct = default);

    /// <summary>Eliminación lógica (Activo = false + FechaActualizacion).</summary>
    Task DeleteLogicoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el máximo sufijo numérico emitido en <c>Codigo</c> (parseado de
    /// <c>CAP-PC-REG-###</c>). 0 si no hay capacitaciones registradas. Incluye filas
    /// lógicamente eliminadas — el código es único global y no se reutiliza.
    /// </summary>
    Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default);

    /// <summary>
    /// Devuelve la <c>FirmaCapacitador</c> más reciente registrada para un capacitador con el
    /// mismo nombre (comparación tolerante a mayúsculas/minúsculas y espacios al inicio/fin), o
    /// <c>null</c> si ningún curso de ese capacitador tiene firma. Se usa para reutilizar la firma
    /// de un profesor que ya firmó antes y no volver a pedírsela al registrarlo en un curso nuevo.
    /// <paramref name="excludeId"/> excluye una capacitación concreta (la propia, al editar).
    /// </summary>
    Task<string?> GetLatestFirmaCapacitadorByNombreAsync(
        string capacitador,
        Guid? excludeId = null,
        CancellationToken ct = default);
}
