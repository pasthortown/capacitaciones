namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Error de validación/negocio del módulo de correo. <see cref="Errores"/> = mensaje por campo.</summary>
public class ConfiguracionCorreoException : Exception
{
    public string Codigo { get; }
    public IReadOnlyDictionary<string, string> Errores { get; }

    public ConfiguracionCorreoException(string codigo, string message, IReadOnlyDictionary<string, string>? errores = null)
        : base(message)
    {
        Codigo = codigo;
        Errores = errores ?? new Dictionary<string, string>();
    }
}
