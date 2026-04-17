namespace Capacitaciones.Application.UseCases;

/// <summary>
/// Excepción de dominio para errores de autenticación / administración de usuarios.
/// El controlador la traduce a un <c>ProblemDetails</c> con el status apropiado.
/// </summary>
public class AuthServiceException : Exception
{
    public string Codigo { get; }

    public AuthServiceException(string codigo, string message) : base(message)
    {
        Codigo = codigo;
    }
}
