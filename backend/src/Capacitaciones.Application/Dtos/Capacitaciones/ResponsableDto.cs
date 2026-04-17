namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// DTO de lectura de un responsable enlazado a una capacitación (detail view).
/// Datos resueltos desde el catálogo global (<c>Responsable</c>) vía la pivote <c>CapacitacionResponsable</c>.
/// La firma es opcional — el responsable puede aún no haberla cargado.
/// </summary>
public class ResponsableDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Firma { get; set; }
    public int Orden { get; set; }
}
