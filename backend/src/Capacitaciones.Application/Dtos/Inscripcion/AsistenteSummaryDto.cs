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
}
