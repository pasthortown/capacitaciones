namespace Capacitaciones.Application.Dtos.Calificaciones;

/// <summary>
/// Proyección compacta de un asistente para la pantalla de calificaciones (Fase 11).
/// Solo se devuelven asistentes con <c>EstadoAsistencia == Presente</c> — no tiene sentido
/// calificar a quien no asistió.
/// </summary>
public class CalificacionesAsistenteDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;

    /// <summary>Siempre "Presente" dado el filtro del use case; se expone para trazabilidad.</summary>
    public string? EstadoAsistencia { get; set; }

    /// <summary>Calificación 0–10 step 0.1. Null si aún no se registró.</summary>
    public decimal? Calificacion { get; set; }
}
