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

    /// <summary>
    /// TTL (en días) del token de inscripción pública emitido por el admin (Fase 5).
    /// Default: 90 días. Mismo valor por defecto que el de capacitador porque ambos links
    /// se distribuyen a personas externas y suelen permanecer activos durante todo el ciclo
    /// de la capacitación.
    /// </summary>
    public int InscripcionTokenDias { get; set; } = 90;

    /// <summary>
    /// TTL (en días) del token del responsable emitido por el admin para que el responsable
    /// complete/actualice su perfil (nombre, cargo, empresa, firma) desde un link público.
    /// Default: 90 días (mismo criterio que los otros tokens de recurso público).
    /// </summary>
    public int ResponsableTokenDias { get; set; } = 90;

    /// <summary>
    /// TTL (en días) del token de pase de lista emitido por el admin para que el capacitador
    /// marque asistencia desde un link público (Fase 10). Default: 90 días.
    /// </summary>
    public int PaseListaTokenDias { get; set; } = 90;

    /// <summary>
    /// TTL (en días) del token de calificaciones emitido por el admin para que el capacitador
    /// registre notas desde un link público (Fase 11). Default: 90 días — mismo criterio que
    /// pase de lista.
    /// </summary>
    public int CalificacionesTokenDias { get; set; } = 90;
}
