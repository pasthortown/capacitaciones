namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Entidad del módulo Repositorio: representa un archivo subido por el admin con
/// metadata asociada. El archivo físico vive en el filesystem del contenedor
/// (volumen configurable vía <c>REPOSITORIO_DIR</c>) y esta entidad guarda únicamente
/// la referencia mediante <see cref="NombreAlmacenado"/> (`{uid}.{ext}`, sin subdirectorio).
///
/// Baja lógica vía <see cref="Activo"/>: el archivo físico sí se elimina al dar de baja
/// (ver <c>EliminarRecursoUseCase</c>). Dado que <see cref="NombreAlmacenado"/> es único
/// en BD, mantener el registro soft-deleted no bloquea futuras subidas porque cada alta
/// genera un <c>Guid.NewGuid()</c> nuevo como storedName.
/// </summary>
public class Recurso
{
    public Guid Id { get; set; }

    /// <summary>Nombre original del archivo subido (tal cual llegó en <c>IFormFile.FileName</c>).</summary>
    public string NombreOriginal { get; set; } = string.Empty;

    /// <summary>
    /// Nombre con el que el archivo se persiste en disco, formato <c>{guid}.{ext}</c> (o solo
    /// <c>{guid}</c> si no hay extensión). Plano, sin subdirectorios.
    /// </summary>
    public string NombreAlmacenado { get; set; } = string.Empty;

    /// <summary>Extensión normalizada en minúsculas y sin punto inicial. Null si el archivo no tenía extensión.</summary>
    public string? Extension { get; set; }

    /// <summary>MIME declarado por el cliente al subir. Informativo (no se valida a fondo).</summary>
    public string? ContentType { get; set; }

    public long TamanoBytes { get; set; }

    /// <summary>Descripción requerida (el admin debe explicar qué contiene el recurso).</summary>
    public string Descripcion { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
