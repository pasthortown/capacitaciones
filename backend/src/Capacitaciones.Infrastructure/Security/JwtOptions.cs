namespace Capacitaciones.Infrastructure.Security;

/// <summary>
/// Opciones de JWT, bindeadas desde la sección <c>Jwt</c> de <c>appsettings</c>.
/// Las variables de entorno <c>JWT_SECRET</c>, <c>JWT_ISSUER</c> y <c>JWT_AUDIENCE</c>
/// también las alimentan (la configuración del entorno se fusiona por encima de appsettings).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary>TTL del token en horas. Default: 8 horas (ver Fase 2 §Auth).</summary>
    public int ExpirationHours { get; set; } = 8;

    /// <summary>
    /// TTL (en días) del token de capacitador emitido por el admin en el link firmado (Fase 4).
    /// Default: 90 días.
    /// </summary>
    public int CapacitadorTokenDias { get; set; } = 90;
}
