namespace Capacitaciones.Application.Dtos.Responsables;

/// <summary>
/// Payload del PUT público del responsable (link firmado).
/// A diferencia del <see cref="UpdateResponsableDto"/> admin, la firma ES requerida acá:
/// el propósito del link es que el responsable cargue/actualice su firma.
/// </summary>
public class UpdateResponsablePerfilDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Firma { get; set; } = string.Empty;
}
