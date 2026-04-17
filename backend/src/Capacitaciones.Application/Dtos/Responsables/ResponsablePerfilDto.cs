namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Vista del responsable desde su propio link firmado. No expone <c>Activo</c>/fechas —
/// solo los campos que el responsable puede revisar/editar.
/// </summary>
public class ResponsablePerfilDto
{
    public Guid Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Firma { get; set; }
}
