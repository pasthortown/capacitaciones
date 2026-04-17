namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>DTO de lectura de un responsable (firmante adicional).</summary>
public class ResponsableDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Firma { get; set; } = string.Empty;
    public int Orden { get; set; }
}
