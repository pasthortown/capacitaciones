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

    /// <summary>Tipo de pregunta — determina cómo se renderiza y se valida la respuesta.</summary>
    public TipoPregunta TipoPregunta { get; set; } = TipoPregunta.SeleccionMultiple;

    /// <summary>
    /// Opciones cuando <see cref="TipoPregunta"/> == <c>SeleccionMultiple</c>. Se guarda como
    /// JSON de <c>string[]</c>. Null o vacío en los demás tipos. Para <c>SiNo</c> las opciones
    /// son fijas ("Sí", "No") y se resuelven en el frontend — no se persisten aquí.
    /// </summary>
    public string? OpcionesJson { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }
}
