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
/// los de recursos públicos (capacitador, inscripción, responsable) — todos firman con el
/// mismo secret/issuer/audience y solo difieren en los claims. Así el DI queda simple y
/// evitamos duplicar la configuración.
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

    /// <summary>
    /// Emite un JWT para el link público de inscripción (Fase 5).
    /// Claims: <c>sub</c> = capacitacionId, <c>role</c> = "Inscripcion",
    /// <c>scope</c> = "inscripcion", <c>cid</c> = capacitacionId.
    /// TTL por defecto: 90 días (configurable vía <c>Jwt:InscripcionTokenDias</c>).
    /// Se mantiene separado del token de capacitador para que las policies de autorización
    /// sean excluyentes (un link de inscripción no puede llamar endpoints del capacitador).
    /// </summary>
    JwtTokenResult GenerateInscripcionToken(Guid capacitacionId);

    /// <summary>
    /// Emite un JWT para el link público del responsable (refactor Responsables a catálogo global).
    /// Claims: <c>sub</c> = responsableId, <c>role</c> = "Responsable",
    /// <c>scope</c> = "responsable", <c>rid</c> = responsableId.
    /// TTL por defecto: 90 días (configurable vía <c>Jwt:ResponsableTokenDias</c>).
    /// </summary>
    JwtTokenResult GenerateResponsableToken(Guid responsableId);

    /// <summary>
    /// Emite un JWT para el link de pase de lista del capacitador (Fase 10).
    /// Claims: <c>sub</c> = capacitacionId, <c>role</c> = "PaseLista",
    /// <c>scope</c> = "pase-lista", <c>cid</c> = capacitacionId.
    /// Se mantiene separado del token de capacitador (descripción/firma) para que un
    /// link filtrado permita solo el flujo para el que fue emitido.
    /// TTL por defecto: 90 días (configurable vía <c>Jwt:PaseListaTokenDias</c>).
    /// </summary>
    JwtTokenResult GeneratePaseListaToken(Guid capacitacionId);

    /// <summary>
    /// Emite un JWT para el link de calificaciones del capacitador (Fase 11).
    /// Claims: <c>sub</c> = capacitacionId, <c>role</c> = "Calificaciones",
    /// <c>scope</c> = "calificaciones", <c>cid</c> = capacitacionId.
    /// Se mantiene independiente del token de pase de lista y del de descripción/firma:
    /// cada link habilita un solo flujo.
    /// TTL por defecto: 90 días (configurable vía <c>Jwt:CalificacionesTokenDias</c>).
    /// </summary>
    JwtTokenResult GenerateCalificacionesToken(Guid capacitacionId);
}
