namespace Capacitaciones.Application.Dtos.Calificaciones;

/// <summary>
/// Respuesta del endpoint admin <c>POST /api/capacitaciones/{id}/link-calificaciones</c> (Fase 11).
/// <c>Url</c> es relativa ("/capacitador/calificaciones?token=..."); el Frontend la resuelve con
/// <c>window.location.origin</c> al copiarla al portapapeles — mismo patrón que los otros links firmados.
/// </summary>
public class LinkCalificacionesResponseDto
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
