namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>DTO de escritura de un responsable (usado tanto en create como en update).</summary>
public class CreateResponsableDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Firma { get; set; } = string.Empty;
    public int Orden { get; set; }
}
