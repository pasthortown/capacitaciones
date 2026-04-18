using Capacitaciones.Application.Dtos.Capacitaciones;

namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Proyección compacta de un <c>Asistente</c> para la pantalla admin de listado y
/// la respuesta <c>201 Created</c> del endpoint público de inscripción.
/// NO incluye la firma (base64 puede ser grande; si se necesita, se consulta por id en un futuro endpoint).
/// </summary>
public class AsistenteSummaryDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;

    /// <summary>Email completo ya con <c>@dos.com.ec</c>.</summary>
    public string Email { get; set; } = string.Empty;

    public CatalogoRefDto Area { get; set; } = new();

    public DateTime FechaInscripcion { get; set; }

    /// <summary>
    /// Fase 10 — pase de lista. "Presente" | "Ausente" | <c>null</c> si aún no se registró.
    /// Se expone como string para que el front lo mapee directamente al componente AttendanceToggle.
    /// </summary>
    public string? EstadoAsistencia { get; set; }

    /// <summary>Fase 10 — timestamp UTC de la última marcación. Null si nunca fue marcado.</summary>
    public DateTime? FechaMarcacionAsistencia { get; set; }

    /// <summary>
    /// Fase 11 — calificación 0–10 step 0.1. Aplica solo cuando la capacitación es
    /// <c>TipoCertificacion == Aprobacion</c> y el asistente está Presente; en cualquier otro
    /// caso se devuelve null. El front renderiza la columna "Calificación" como editable solo
    /// cuando corresponde.
    /// </summary>
    public decimal? Calificacion { get; set; }
}
