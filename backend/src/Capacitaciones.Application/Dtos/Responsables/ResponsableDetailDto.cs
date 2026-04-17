namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Detalle completo de un responsable (incluye la firma base64). Usado al obtener
/// un responsable por id en el admin.
/// </summary>
public class ResponsableDetailDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public bool TieneFirma { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public string? Firma { get; set; }
}
