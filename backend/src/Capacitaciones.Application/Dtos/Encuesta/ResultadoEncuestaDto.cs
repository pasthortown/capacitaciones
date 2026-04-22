namespace Capacitaciones.Application.Dtos.Encuesta;

/// <summary>
/// Datos agregados de la encuesta de una capacitación para el dashboard admin.
/// Incluye el resumen por pregunta según su tipo: conteo por opción para
/// SeleccionMultiple / SiNo, lista de comentarios para TextoLargo.
/// </summary>
public class ResultadoEncuestaDto
{
    public Guid CapacitacionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public string? Capacitador { get; set; }
    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }
    public string TipoActividadNombre { get; set; } = string.Empty;

    /// <summary>Cantidad total de asistentes inscritos a la capacitación.</summary>
    public int TotalAsistentes { get; set; }

    /// <summary>Cantidad de asistentes distintos que enviaron la encuesta.</summary>
    public int TotalRespondieron { get; set; }

    public IReadOnlyList<ResultadoPreguntaDto> Preguntas { get; set; } = Array.Empty<ResultadoPreguntaDto>();
}

public class ResultadoPreguntaDto
{
    public Guid Id { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string TipoPregunta { get; set; } = string.Empty;

    /// <summary>Opciones declaradas en la pregunta (SeleccionMultiple) o ["Si","No"] para SiNo.</summary>
    public IReadOnlyList<string> Opciones { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Conteo absoluto por opción. Para SeleccionMultiple/SiNo. Garantiza que todas las opciones
    /// declaradas aparezcan (aunque tengan 0 votos), más una entrada "Otras" si existe respuesta
    /// que no coincide con ninguna opción (defensivo por cambios en opciones históricas).
    /// </summary>
    public IReadOnlyList<ConteoOpcionDto> ConteoOpciones { get; set; } = Array.Empty<ConteoOpcionDto>();

    /// <summary>Lista de comentarios para TextoLargo (asistente + texto). Vacía en otros tipos.</summary>
    public IReadOnlyList<RespuestaTextoDto> RespuestasTexto { get; set; } = Array.Empty<RespuestaTextoDto>();

    public int TotalRespuestas { get; set; }
}

public class ConteoOpcionDto
{
    public string Opcion { get; set; } = string.Empty;
    public int Conteo { get; set; }
}

public class RespuestaTextoDto
{
    public string Asistente { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public DateTime FechaRespuesta { get; set; }
}
