using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Security;

/// <summary>
/// Adaptador de <see cref="IPasswordHasher"/> basado en BCrypt.Net-Next.
/// Factor de trabajo: 12 (balance estándar entre costo y seguridad).
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash con formato inválido: tratar como credencial incorrecta.
            return false;
        }
    }
}
