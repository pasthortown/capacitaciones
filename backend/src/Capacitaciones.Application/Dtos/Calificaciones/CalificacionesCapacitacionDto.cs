namespace Capacitaciones.Application.Dtos.Calificaciones;

/// <summary>
/// Resumen read-only de la capacitación para la pantalla de calificaciones (Fase 11).
/// Incluye <c>TipoCertificacion</c> y <c>PuntajeMinimo</c> para que el front resalte
/// en verde/rojo cada calificación según apruebe o no.
/// </summary>
public class CalificacionesCapacitacionDto
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Tema { get; set; } = string.Empty;
    public DateTime FechaHoraInicio { get; set; }
    public int DuracionMinutos { get; set; }
    public string Estado { get; set; } = string.Empty;

    /// <summary>"Participacion" | "Aprobacion". Solo se emite el link si es "Aprobacion".</summary>
    public string TipoCertificacion { get; set; } = string.Empty;

    /// <summary>Puntaje mínimo para aprobar (0–10). Null si la capacitación no lo configuró aún.</summary>
    public decimal? PuntajeMinimo { get; set; }
}
