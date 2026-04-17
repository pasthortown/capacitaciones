namespace Capacitaciones.Application.Dtos.Capacitador;

/// <summary>
/// Respuesta del endpoint admin <c>POST /api/capacitaciones/{id}/link-capacitador</c>.
/// <c>Url</c> es relativa ("/capacitador?token=..."); el Frontend la resuelve con
/// <c>window.location.origin</c> al copiarla al portapapeles.
/// </summary>
public class LinkCapacitadorResponseDto
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
