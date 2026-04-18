namespace Capacitaciones.Application.Dtos.Recursos;

/// <summary>
/// Respuesta del endpoint admin <c>POST /api/recursos/{id}/link</c>.
/// <c>Url</c> es relativa: el Frontend la resuelve contra <c>window.location.origin</c>
/// al copiarla al portapapeles. El endpoint destino es público (no requiere auth).
/// </summary>
public class LinkDescargaRecursoDto
{
    public string Url { get; set; } = string.Empty;
    public Guid RecursoId { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public string? ContentType { get; set; }
}
