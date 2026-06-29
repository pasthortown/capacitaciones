namespace Capacitaciones.Application.UseCases.Colaboradores;

/// <summary>
/// Excepción base de negocio del módulo Colaboradores. El controlador traduce <see cref="Codigo"/>
/// a un HTTP status apropiado.
/// </summary>
public class ColaboradorServiceException : Exception
{
    public string Codigo { get; }

    public ColaboradorServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

/// <summary>El colaborador externo no existe.</summary>
public class ColaboradorNotFoundException : ColaboradorServiceException
{
    public ColaboradorNotFoundException(Guid id)
        : base("NOT_FOUND", $"No existe un colaborador externo con Id={id}.") { }
}

/// <summary>Ya hay un externo con esa cédula.</summary>
public class CedulaDuplicadaException : ColaboradorServiceException
{
    public CedulaDuplicadaException(string cedula)
        : base("CEDULA_DUPLICADA", $"Ya existe un colaborador externo con la cédula {cedula}.") { }
}

/// <summary>
/// La cédula ya pertenece a un colaborador de DOS (existe en ControlTareas), así que no puede
/// registrarse como externo. Regla: los de DOS se administran en ControlTareas, no aquí.
/// </summary>
public class CedulaPerteneceADosException : ColaboradorServiceException
{
    public CedulaPerteneceADosException(string cedula)
        : base("CEDULA_PERTENECE_A_DOS",
            $"La cédula {cedula} pertenece a un colaborador de DOS (ControlTareas) y no puede registrarse como externo.") { }
}

/// <summary>Validación de datos del colaborador externo.</summary>
public class ColaboradorValidacionException : ColaboradorServiceException
{
    public ColaboradorValidacionException(string mensaje) : base("VALIDACION", mensaje) { }
}
