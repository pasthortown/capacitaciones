namespace Capacitaciones.Application.Dtos.Encuesta;

public class UpsertPreguntaEncuestaDto
{
    public Guid TipoActividadId { get; set; }
    public string Texto { get; set; } = string.Empty;

    /// <summary>"SeleccionMultiple" | "TextoLargo" | "SiNo".</summary>
    public string TipoPregunta { get; set; } = "SeleccionMultiple";

    /// <summary>Opciones cuando TipoPregunta = SeleccionMultiple. Ignorado en los demás.</summary>
    public IReadOnlyList<string> Opciones { get; set; } = Array.Empty<string>();

    public bool Activo { get; set; } = true;
}
