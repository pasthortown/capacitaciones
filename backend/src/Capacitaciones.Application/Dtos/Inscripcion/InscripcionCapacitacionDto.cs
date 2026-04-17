using Capacitaciones.Application.Dtos.Capacitaciones;

namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Vista pública de una capacitación para la pantalla de inscripción (Fase 5).
/// Contiene solo lo imprescindible para que el asistente verifique dónde se está
/// inscribiendo. NO incluye responsables ni lista de asistentes.
/// </summary>
public class InscripcionCapacitacionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public string Capacitador { get; set; } = string.Empty;

    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    public CatalogoRefDto Modalidad { get; set; } = new();
    public CatalogoRefDto TipoActividad { get; set; } = new();

    /// <summary>Estado derivado: "Inscripciones Abiertas" | "Iniciada" | "Finalizada".</summary>
    public string Estado { get; set; } = string.Empty;
}
