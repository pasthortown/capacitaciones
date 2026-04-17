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
///
/// Decisión Fase 4: se mantiene un solo puerto para emitir tanto el token de admin como
/// el del capacitador — ambos se firman con el mismo secret/issuer/audience y solo
/// difieren en los claims. Así el DI queda simple (un único <c>IJwtTokenGenerator</c>) y
/// evitamos duplicar la configuración en otro puerto.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Emite un JWT con claims <c>sub</c>=id, <c>email</c>, <c>name</c>=nombres, <c>role</c>=Admin.
    /// TTL por defecto: 8 horas.
    /// </summary>
    JwtTokenResult Generate(AdminUser user);

    /// <summary>
    /// Emite un JWT de capacitador para una capacitación dada.
    /// Claims: <c>sub</c> = capacitacionId, <c>role</c> = "Capacitador",
    /// <c>scope</c> = "capacitador", <c>cid</c> = capacitacionId.
    /// TTL por defecto: 90 días (configurable vía <c>Jwt:CapacitadorTokenDias</c>).
    /// </summary>
    JwtTokenResult GenerateCapacitadorToken(Guid capacitacionId);
}
