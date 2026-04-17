namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Proyección ligera del catálogo de responsables para listas del admin.
/// No expone la firma (puede ser un base64 muy grande); solo el flag <c>TieneFirma</c>.
/// </summary>
public class ResponsableSummaryDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public bool TieneFirma { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
