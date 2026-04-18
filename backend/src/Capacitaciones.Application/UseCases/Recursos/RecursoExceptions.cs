namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Excepción base de negocio del módulo Repositorio. El controlador traduce
/// <see cref="Codigo"/> a un HTTP status apropiado.
/// </summary>
public class RecursoServiceException : Exception
{
    public string Codigo { get; }

    public RecursoServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

/// <summary>Se lanza cuando un recurso no existe o está inactivo y la operación lo requiere activo.</summary>
public class RecursoNotFoundException : RecursoServiceException
{
    public RecursoNotFoundException(Guid id)
        : base("NOT_FOUND", $"No existe un recurso con Id={id}.") { }
}

/// <summary>
/// Se lanza si la metadata existe pero el archivo físico no está en el storage.
/// Indica inconsistencia operativa (ej: volumen borrado manualmente); el controller
/// responde 410 Gone para diferenciarlo de 404.
/// </summary>
public class ArchivoFisicoAusenteException : RecursoServiceException
{
    public ArchivoFisicoAusenteException(Guid recursoId, string storedName)
        : base("ARCHIVO_FISICO_AUSENTE",
            $"El recurso {recursoId} existe en BD pero el archivo físico '{storedName}' no está disponible.") { }
}
