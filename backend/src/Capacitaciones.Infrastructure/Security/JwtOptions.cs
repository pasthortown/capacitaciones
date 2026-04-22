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
    /// TTL (en horas) del token de capacitador emitido por el admin en el link firmado (Fase 4).
    /// Default: 48 horas — los enlaces públicos son de corta vida por seguridad.
    /// </summary>
    public int CapacitadorTokenHoras { get; set; } = 48;

    /// <summary>
    /// TTL (en horas) del token de inscripción pública emitido por el admin (Fase 5).
    /// Default: 48 horas.
    /// </summary>
    public int InscripcionTokenHoras { get; set; } = 48;

    /// <summary>
    /// TTL (en horas) del token del responsable emitido por el admin para que el responsable
    /// complete/actualice su perfil (nombre, cargo, empresa, firma) desde un link público.
    /// Default: 48 horas.
    /// </summary>
    public int ResponsableTokenHoras { get; set; } = 48;

    /// <summary>
    /// TTL (en horas) del token de pase de lista emitido por el admin para que el capacitador
    /// marque asistencia desde un link público (Fase 10). Default: 48 horas.
    /// </summary>
    public int PaseListaTokenHoras { get; set; } = 48;

    /// <summary>
    /// TTL (en horas) del token de calificaciones emitido por el admin para que el capacitador
    /// registre notas desde un link público (Fase 11). Default: 48 horas.
    /// </summary>
    public int CalificacionesTokenHoras { get; set; } = 48;
}
