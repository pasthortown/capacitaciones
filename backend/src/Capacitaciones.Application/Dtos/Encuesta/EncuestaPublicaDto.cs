namespace Capacitaciones.Application.Dtos.Encuesta;

/// <summary>
/// Payload que recibe la página pública de encuesta para una capacitación finalizada.
/// Incluye el header de la capacitación (para contexto visual) y la lista de preguntas
/// activas aplicables al tipo de actividad.
/// </summary>
public class EncuestaPublicaDto
{
    public Guid CapacitacionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public string? Capacitador { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }
    public string TipoActividadNombre { get; set; } = string.Empty;
    public IReadOnlyList<EncuestaPreguntaDto> Preguntas { get; set; } = Array.Empty<EncuestaPreguntaDto>();
}

public class EncuestaPreguntaDto
{
    public Guid Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string TipoPregunta { get; set; } = string.Empty;
    public IReadOnlyList<string> Opciones { get; set; } = Array.Empty<string>();
}
