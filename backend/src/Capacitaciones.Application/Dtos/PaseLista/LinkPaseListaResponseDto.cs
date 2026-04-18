namespace Capacitaciones.Application.Dtos.PaseLista;

/// <summary>
/// Respuesta del endpoint admin <c>POST /api/capacitaciones/{id}/link-pase-lista</c> (Fase 10).
/// <c>Url</c> es relativa ("/capacitador/pase-lista?token=..."); el Frontend la resuelve con
/// <c>window.location.origin</c> al copiarla al portapapeles — mismo patrón que los demás links firmados.
/// </summary>
public class LinkPaseListaResponseDto
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
