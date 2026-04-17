using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.Ports;

/// <summary>Resultado de <see cref="IJwtTokenGenerator.Generate"/>.</summary>
public class JwtTokenResult
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Abstracción para emitir JWTs firmados. La implementación concreta vive en Infrastructure
/// y firma con HMAC-SHA256 usando <c>Jwt:Secret</c>.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Emite un JWT con claims <c>sub</c>=id, <c>email</c>, <c>name</c>=nombres, <c>role</c>=Admin.
    /// TTL por defecto: 8 horas.
    /// </summary>
    JwtTokenResult Generate(AdminUser user);
}
