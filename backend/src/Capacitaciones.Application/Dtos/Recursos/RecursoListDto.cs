namespace Capacitaciones.Application.Dtos.Recursos;

/// <summary>
/// Proyección de lectura para el listado del repositorio. No expone el nombre interno
/// en disco (<c>NombreAlmacenado</c>) — ese detalle se reserva para el detalle admin.
/// </summary>
public class RecursoListDto
{
    public Guid Id { get; set; }
    public string NombreOriginal { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? ContentType { get; set; }
    public long TamanoBytes { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
