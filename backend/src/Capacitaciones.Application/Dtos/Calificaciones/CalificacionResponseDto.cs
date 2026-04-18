namespace Capacitaciones.Application.Dtos.Calificaciones;

/// <summary>
/// Respuesta compacta tras registrar una calificación (Fase 11). Permite al front actualizar
/// únicamente el asistente tocado sin volver a pedir toda la lista.
/// </summary>
public class CalificacionResponseDto
{
    public Guid Id { get; set; }

    /// <summary>Calificación persistida (0–10). Null si se limpió.</summary>
    public decimal? Calificacion { get; set; }
}
