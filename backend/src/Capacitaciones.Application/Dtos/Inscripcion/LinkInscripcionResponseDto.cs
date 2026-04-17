namespace Capacitaciones.Application.Dtos.Inscripcion;

/// <summary>
/// Respuesta del endpoint admin <c>POST /api/capacitaciones/{id}/link-inscripcion</c>.
/// <c>Url</c> es relativa ("/inscripcion?token=..."); el frontend la resuelve con
/// <c>window.location.origin</c> al copiarla al portapapeles.
/// </summary>
public class LinkInscripcionResponseDto
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
