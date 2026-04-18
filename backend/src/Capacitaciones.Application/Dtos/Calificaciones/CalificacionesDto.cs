namespace Capacitaciones.Application.Dtos.Calificaciones;

/// <summary>
/// Respuesta del endpoint público <c>GET /api/capacitador/calificaciones</c> (Fase 11).
/// Incluye los datos básicos de la capacitación (con <c>PuntajeMinimo</c> para pintar umbral)
/// y la lista de asistentes Presentes ordenada alfabéticamente por Apellidos/Nombres.
/// </summary>
public class CalificacionesDto
{
    public CalificacionesCapacitacionDto Capacitacion { get; set; } = new();

    public IReadOnlyList<CalificacionesAsistenteDto> Asistentes { get; set; } = new List<CalificacionesAsistenteDto>();
}
