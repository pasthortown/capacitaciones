namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Pregunta de encuesta de satisfacción asociada a un tipo de actividad.
/// Al finalizar una capacitación, se muestran a los asistentes las preguntas
/// activas cuyo <c>TipoActividadId</c> coincida con el de la capacitación.
/// </summary>
public class PreguntaEncuesta
{
    public Guid Id { get; set; }

    public Guid TipoActividadId { get; set; }

    public TipoActividad? TipoActividad { get; set; }

    public string Texto { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}
