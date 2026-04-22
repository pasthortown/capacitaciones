namespace Capacitaciones.Application.Dtos.Encuesta;

public class PreguntaEncuestaDto
{
    public Guid Id { get; set; }
    public Guid TipoActividadId { get; set; }
    public string TipoActividadNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public string TipoPregunta { get; set; } = string.Empty;
    public IReadOnlyList<string> Opciones { get; set; } = Array.Empty<string>();
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
