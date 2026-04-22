namespace Capacitaciones.Application.Dtos.Encuesta;

/// <summary>
/// Cuerpo del POST que envía el asistente para responder la encuesta.
/// El asistente se autoidentifica con <see cref="Identificacion"/> (cédula),
/// el backend valida que pertenezca a la capacitación y que no haya respondido antes.
/// </summary>
public class SubmitEncuestaDto
{
    public string Identificacion { get; set; } = string.Empty;
    public IReadOnlyList<RespuestaItemDto> Respuestas { get; set; } = Array.Empty<RespuestaItemDto>();
}

public class RespuestaItemDto
{
    public Guid PreguntaEncuestaId { get; set; }
    public string Respuesta { get; set; } = string.Empty;
}
