namespace Capacitaciones.Application.UseCases.Catalogos;

/// <summary>
/// Errores de negocio al manipular catálogos (p. ej. nombre duplicado, nombre inválido).
/// </summary>
public class CatalogoServiceException : Exception
{
    public string Codigo { get; }

    public CatalogoServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

/// <summary>
/// Se lanza cuando se intenta operar sobre un catálogo que no existe.
/// </summary>
public class CatalogoNotFoundException : CatalogoServiceException
{
    public CatalogoNotFoundException(Guid id)
        : base("NOT_FOUND", $"No existe un ítem de catálogo con Id={id}.") { }
}
