namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Respuesta de un asistente a una pregunta de la encuesta de satisfacción.
/// Escala Likert 1..5 (1=Muy insatisfecho, 5=Muy satisfecho). Un asistente
/// responde a cada pregunta exactamente una vez por capacitación.
/// </summary>
public class RespuestaEncuesta
{
    public Guid Id { get; set; }

    public Guid AsistenteId { get; set; }

    public Asistente? Asistente { get; set; }

    public Guid PreguntaEncuestaId { get; set; }

    public PreguntaEncuesta? PreguntaEncuesta { get; set; }

    public int Valor { get; set; }

    public DateTime FechaRespuesta { get; set; }
}
