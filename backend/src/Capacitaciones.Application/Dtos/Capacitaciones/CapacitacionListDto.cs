namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Proyección ligera para el grid de cards del dashboard.
/// <c>Estado</c> se calcula en tiempo de mapeo y <c>TotalAsistentes</c> es un stub
/// (devuelve 0 hasta Fase 5, cuando exista la tabla Asistente).
/// </summary>
public class CapacitacionListDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public string Capacitador { get; set; } = string.Empty;

    public CatalogoRefDto Modalidad { get; set; } = new();
    public CatalogoRefDto TipoActividad { get; set; } = new();

    public string TipoCertificacion { get; set; } = string.Empty;

    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    /// <summary>Inscripciones Abiertas / Iniciada / Finalizada.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>TODO Fase 5: contar filas de Asistente por capacitacionId.</summary>
    public int TotalAsistentes { get; set; }

    public bool Activo { get; set; }
}
