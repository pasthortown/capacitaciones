namespace Capacitaciones.Application.Ports;

/// <summary>
/// Cifra/descifra secretos que se guardan en base de datos (ej. la contraseña SMTP de
/// <c>ConfiguracionCorreo</c>). La llave vive fuera de la BD (variable de entorno).
/// </summary>
public interface ISecretProtector
{
    /// <summary>false si no hay llave configurada: <see cref="Protect"/> lanzará.</summary>
    bool IsConfigured { get; }

    /// <summary>Cifra <paramref name="plain"/>. Lanza <see cref="InvalidOperationException"/> sin llave.</summary>
    string Protect(string plain);

    /// <summary>Descifra; devuelve null si no hay llave, el texto está corrupto o se cifró con otra llave.</summary>
    string? TryUnprotect(string cipher);
}
