namespace Capacitaciones.Application.Dtos.Encuesta;

public class UpsertPreguntaEncuestaDto
{
    public Guid TipoActividadId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
