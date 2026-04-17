namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Errores de negocio al manipular el catálogo de responsables (validación, firma inválida, etc.).
/// El controlador traduce el <c>Codigo</c> a un HTTP adecuado.
/// </summary>
public class ResponsableServiceException : Exception
{
    public string Codigo { get; }

    public ResponsableServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

/// <summary>Se lanza cuando un responsable no existe y no se puede operar sobre él.</summary>
public class ResponsableNotFoundException : ResponsableServiceException
{
    public ResponsableNotFoundException(Guid id)
        : base("NOT_FOUND", $"No existe un responsable con Id={id}.") { }
}
