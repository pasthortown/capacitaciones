namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Errores de negocio al manipular capacitaciones (validación, catálogo inexistente,
/// duración inválida, etc.). El controlador traduce el <c>Codigo</c> a un HTTP adecuado.
/// </summary>
public class CapacitacionServiceException : Exception
{
    public string Codigo { get; }

    public CapacitacionServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

/// <summary>Se lanza cuando una capacitación no existe o está inactiva y no se puede operar sobre ella.</summary>
public class CapacitacionNotFoundException : CapacitacionServiceException
{
    public CapacitacionNotFoundException(Guid id)
        : base("NOT_FOUND", $"No existe una capacitación con Id={id}.") { }
}
