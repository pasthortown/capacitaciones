namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class PreguntaEncuestaServiceException : Exception
{
    public string Codigo { get; }

    public PreguntaEncuestaServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}

public class PreguntaEncuestaNotFoundException : PreguntaEncuestaServiceException
{
    public PreguntaEncuestaNotFoundException(Guid id)
        : base("NOT_FOUND", $"No existe una pregunta de encuesta con Id={id}.") { }
}
