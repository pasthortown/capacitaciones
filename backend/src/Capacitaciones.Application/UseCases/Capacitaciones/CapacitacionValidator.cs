using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Valida los campos comunes del create y del update. Lanza <see cref="CapacitacionServiceException"/>
/// con códigos estables para que el controlador los traduzca a HTTP.
/// </summary>
internal static class CapacitacionValidator
{
    public const int TemaMaxLength = 500;
    public const int CapacitadorMaxLength = 255;

    public static void ValidarTema(string? tema)
    {
        if (string.IsNullOrWhiteSpace(tema))
            throw new CapacitacionServiceException("INVALID_TEMA", "El campo 'tema' es requerido.");
        if (tema.Length > TemaMaxLength)
            throw new CapacitacionServiceException("INVALID_TEMA", $"'tema' excede el máximo de {TemaMaxLength} caracteres.");
    }

    public static void ValidarCapacitador(string? capacitador)
    {
        if (string.IsNullOrWhiteSpace(capacitador))
            throw new CapacitacionServiceException("INVALID_CAPACITADOR", "El campo 'capacitador' es requerido.");
        if (capacitador.Length > CapacitadorMaxLength)
            throw new CapacitacionServiceException("INVALID_CAPACITADOR", $"'capacitador' excede el máximo de {CapacitadorMaxLength} caracteres.");
    }

    public static void ValidarDuracion(int duracionMinutos)
    {
        if (duracionMinutos <= 0)
            throw new CapacitacionServiceException("INVALID_DURACION", "'duracionMinutos' debe ser mayor a 0.");
        if (duracionMinutos % 30 != 0)
            throw new CapacitacionServiceException("INVALID_DURACION", "'duracionMinutos' debe ser múltiplo de 30.");
    }

    public static TipoCertificacion ParsearTipoCertificacion(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new CapacitacionServiceException("INVALID_TIPO_CERTIFICACION", "'tipoCertificacion' es requerido.");

        // Aceptamos tanto el nombre como el int (resistente a clientes que manden "1"/"2").
        if (Enum.TryParse<TipoCertificacion>(valor, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(TipoCertificacion), parsed))
        {
            return parsed;
        }

        throw new CapacitacionServiceException(
            "INVALID_TIPO_CERTIFICACION",
            "'tipoCertificacion' debe ser 'Participacion' o 'Aprobacion'.");
    }

    public static async Task ValidarCatalogosAsync(
        Guid modalidadId,
        Guid tipoActividadId,
        IModalidadRepository modalidades,
        ITipoActividadRepository tiposActividad,
        CancellationToken ct)
    {
        if (modalidadId == Guid.Empty)
            throw new CapacitacionServiceException("INVALID_MODALIDAD", "'modalidadId' es requerido.");

        var modalidad = await modalidades.GetByIdAsync(modalidadId, ct);
        if (modalidad is null || !modalidad.Activo)
        {
            throw new CapacitacionServiceException(
                "INVALID_MODALIDAD",
                $"La modalidad con Id={modalidadId} no existe o está inactiva.");
        }

        if (tipoActividadId == Guid.Empty)
            throw new CapacitacionServiceException("INVALID_TIPO_ACTIVIDAD", "'tipoActividadId' es requerido.");

        var tipo = await tiposActividad.GetByIdAsync(tipoActividadId, ct);
        if (tipo is null || !tipo.Activo)
        {
            throw new CapacitacionServiceException(
                "INVALID_TIPO_ACTIVIDAD",
                $"El tipo de actividad con Id={tipoActividadId} no existe o está inactivo.");
        }
    }

    /// <summary>
    /// Valida los <c>responsableIds</c> del payload: ningún Guid.Empty y ningún id duplicado.
    /// La existencia/actividad se valida en el UseCase (consulta al repo).
    /// </summary>
    public static void ValidarResponsableIds(IEnumerable<Guid>? responsableIds)
    {
        if (responsableIds is null) return;

        var seen = new HashSet<Guid>();
        foreach (var id in responsableIds)
        {
            if (id == Guid.Empty)
                throw new CapacitacionServiceException("INVALID_RESPONSABLE", "'responsableIds' contiene un id vacío.");
            if (!seen.Add(id))
            {
                throw new CapacitacionServiceException(
                    "RESPONSABLE_DUPLICADO",
                    $"El responsable con Id={id} aparece más de una vez en la lista.");
            }
        }
    }

    /// <summary>
    /// Valida contra el catálogo que todos los ids existen y están activos. Lanza
    /// <see cref="CapacitacionServiceException"/> con código <c>INVALID_RESPONSABLE</c> si no.
    /// </summary>
    public static async Task ValidarResponsablesActivosAsync(
        IEnumerable<Guid>? responsableIds,
        IResponsableRepository responsables,
        CancellationToken ct)
    {
        if (responsableIds is null) return;
        var ids = responsableIds.ToList();
        if (ids.Count == 0) return;

        var ok = await responsables.ExistenActivosAsync(ids, ct);
        if (!ok)
        {
            throw new CapacitacionServiceException(
                "INVALID_RESPONSABLE",
                "Uno o más 'responsableIds' no existen o están inactivos.");
        }
    }
}
