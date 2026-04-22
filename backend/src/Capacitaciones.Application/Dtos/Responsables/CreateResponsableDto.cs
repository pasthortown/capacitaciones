namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Payload admin para crear un responsable. La firma es opcional — el admin puede
/// registrar solo datos y dejar que el propio responsable la cargue después vía link firmado.
/// </summary>
public class CreateResponsableDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Firma { get; set; }
}
