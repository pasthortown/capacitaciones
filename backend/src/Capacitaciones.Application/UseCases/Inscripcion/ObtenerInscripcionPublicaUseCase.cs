using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Inscripcion;

/// <summary>
/// Carga la vista pública de la capacitación + las áreas activas para alimentar el formulario.
///
/// Reglas (ver §7.2.5):
///   - Capacitación inexistente → <see cref="CapacitacionNotFoundException"/> (404).
///   - Capacitación inactiva → <see cref="CapacitacionServiceException"/> con código <c>CAPACITACION_INACTIVA</c> (409).
///   - Capacitación <c>Finalizada</c> → <see cref="InscripcionCerradaException"/> (409) — ya no acepta registros.
///   - "Iniciada" SÍ se permite (el usuario llegó tarde pero puede registrarse mientras no termine).
/// </summary>
public class ObtenerInscripcionPublicaUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAreaRepository _areas;

    public ObtenerInscripcionPublicaUseCase(ICapacitacionRepository capacitaciones, IAreaRepository areas)
    {
        _capacitaciones = capacitaciones;
        _areas = areas;
    }

    public async Task<InscripcionPublicaVistaDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var entity = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!entity.Activo)
        {
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "La capacitación está inactiva.");
        }

        var estado = CapacitacionEstadoCalculator.Calcular(entity);
        if (estado == CapacitacionEstadoCalculator.Finalizada)
        {
            throw new InscripcionCerradaException();
        }

        var areas = await _areas.ListAsync(includeInactive: false, ct);

        return new InscripcionPublicaVistaDto
        {
            Capacitacion = MapCapacitacion(entity, estado),
            Areas = areas
                .OrderBy(a => a.Nombre)
                .Select(a => new CatalogoRefDto { Id = a.Id, Nombre = a.Nombre })
                .ToList()
        };
    }

    internal static InscripcionCapacitacionDto MapCapacitacion(Capacitacion c, string estado) => new()
    {
        Codigo = c.Codigo,
        Tema = c.Tema,
        Capacitador = c.Capacitador,
        FechaHoraInicio = c.FechaHoraInicio,
        DuracionMinutos = c.DuracionMinutos,
        Modalidad = new CatalogoRefDto { Id = c.ModalidadId, Nombre = c.Modalidad?.Nombre ?? string.Empty },
        TipoActividad = new CatalogoRefDto { Id = c.TipoActividadId, Nombre = c.TipoActividad?.Nombre ?? string.Empty },
        Estado = estado
    };
}
