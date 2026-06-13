namespace Capacitaciones.Application.Common;

/// <summary>
/// Convierte DateTime UTC al huso horario de Ecuador (America/Guayaquil, UTC-5).
/// Necesario para presentación en correos y reportes: el container backend corre en UTC,
/// por lo que <c>DateTime.ToLocalTime()</c> es no-op y muestra la hora UTC al usuario.
/// Toda la persistencia sigue siendo UTC (regla del ValueConverter en AppDbContext).
/// </summary>
public static class EcuadorTime
{
    private static readonly TimeZoneInfo Tz = ResolveTz();

    /// <summary>
    /// Devuelve el DateTime equivalente en hora Ecuador. Asume Kind=Utc; si llega
    /// Unspecified, lo trata como UTC (consistente con el ValueConverter de EF Core).
    /// </summary>
    public static DateTime FromUtc(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc
            ? dt
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Tz);
    }

    private static TimeZoneInfo ResolveTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
    }
}
