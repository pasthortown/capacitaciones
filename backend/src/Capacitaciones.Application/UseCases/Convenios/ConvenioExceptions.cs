namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Excepción base de negocio del módulo Convenios. El controlador traduce el código a HTTP.</summary>
public class ConvenioServiceException : Exception
{
    public string Codigo { get; }
    public ConvenioServiceException(string codigo, string mensaje) : base(mensaje) { Codigo = codigo; }
}

public class ConvenioNotFoundException : ConvenioServiceException
{
    public ConvenioNotFoundException(Guid id) : base("NOT_FOUND", $"No existe un convenio con Id={id}.") { }
}

public class ConvenioValidacionException : ConvenioServiceException
{
    public ConvenioValidacionException(string mensaje) : base("VALIDACION", mensaje) { }
}

/// <summary>La cédula del convenio no corresponde a ningún colaborador (DOS ni externo).</summary>
public class ColaboradorNoEncontradoException : ConvenioServiceException
{
    public ColaboradorNoEncontradoException(string cedula)
        : base("COLABORADOR_NO_ENCONTRADO",
            $"No existe un colaborador (DOS ni externo) con la cédula {cedula}.") { }
}
