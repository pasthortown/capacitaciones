namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Respuesta de un asistente a una pregunta de la encuesta de satisfacción.
/// El contenido se guarda como string: para SeleccionMultiple el texto de la
/// opción elegida, para SiNo "Si" | "No", para TextoLargo el comentario libre.
/// Un asistente responde a cada pregunta exactamente una vez por capacitación.
/// </summary>
public class RespuestaEncuesta
{
    public Guid Id { get; set; }

    public Guid AsistenteId { get; set; }

    public Asistente? Asistente { get; set; }

    public Guid PreguntaEncuestaId { get; set; }

    public PreguntaEncuesta? PreguntaEncuesta { get; set; }

    /// <summary>Respuesta como texto. Interpretación depende de PreguntaEncuesta.TipoPregunta.</summary>
    public string Respuesta { get; set; } = string.Empty;

    public DateTime FechaRespuesta { get; set; }
}
