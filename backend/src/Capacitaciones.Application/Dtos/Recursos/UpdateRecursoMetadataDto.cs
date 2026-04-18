namespace Capacitaciones.Application.Dtos.Recursos;

/// <summary>
/// Payload admin para editar la metadata de un recurso. No permite reemplazar el
/// archivo físico — para eso el admin debe borrar y volver a subir.
/// </summary>
public class UpdateRecursoMetadataDto
{
    public string NombreOriginal { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}
