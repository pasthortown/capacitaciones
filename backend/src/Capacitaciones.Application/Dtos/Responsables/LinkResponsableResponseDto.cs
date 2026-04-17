namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Respuesta del endpoint admin <c>POST /api/responsables/{id}/link</c>.
/// <c>Url</c> es relativa ("/responsable?token=..."); el Frontend la resuelve con
/// <c>window.location.origin</c> al copiarla al portapapeles.
/// </summary>
public class LinkResponsableResponseDto
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
