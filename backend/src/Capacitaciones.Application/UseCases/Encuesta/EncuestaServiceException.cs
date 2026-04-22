namespace Capacitaciones.Application.UseCases.Encuesta;

public class EncuestaServiceException : Exception
{
    public string Codigo { get; }

    public EncuestaServiceException(string codigo, string mensaje) : base(mensaje)
    {
        Codigo = codigo;
    }
}
