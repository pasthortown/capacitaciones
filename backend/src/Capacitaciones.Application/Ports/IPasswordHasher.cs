namespace Capacitaciones.Application.Ports;

/// <summary>
/// Abstracción para hash + verificación de contraseñas. La implementación concreta
/// vive en Infrastructure y usa BCrypt.Net-Next con factor 12.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
