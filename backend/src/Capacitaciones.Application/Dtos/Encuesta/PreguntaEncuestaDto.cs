namespace Capacitaciones.Application.Dtos.Encuesta;

public class PreguntaEncuestaDto
{
    public Guid Id { get; set; }
    public Guid TipoActividadId { get; set; }
    public string TipoActividadNombre { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
