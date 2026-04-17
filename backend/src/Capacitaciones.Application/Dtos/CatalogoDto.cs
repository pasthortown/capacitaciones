namespace Capacitaciones.Application.Dtos;

/// <summary>
/// DTO de lectura para un ítem de catálogo administrable.
/// </summary>
public class CatalogoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
