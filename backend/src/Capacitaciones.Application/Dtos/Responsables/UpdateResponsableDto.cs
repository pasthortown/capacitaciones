namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Payload admin para editar un responsable. Mismo shape que <see cref="CreateResponsableDto"/>.
/// Firma opcional: si el admin envía null conserva la firma existente; si envía "" o whitespace se limpia.
/// </summary>
public class UpdateResponsableDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string? Firma { get; set; }
}
