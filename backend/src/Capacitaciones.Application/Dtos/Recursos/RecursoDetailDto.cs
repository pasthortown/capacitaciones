namespace Capacitaciones.Application.Dtos.Recursos;

/// <summary>
/// Detalle admin de un recurso. Incluye el <c>NombreAlmacenado</c> para depuración
/// (permite localizar el archivo en el volumen <c>REPOSITORIO_DIR</c>).
/// </summary>
public class RecursoDetailDto
{
    public Guid Id { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string NombreAlmacenado { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? ContentType { get; set; }
    public long TamanoBytes { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
