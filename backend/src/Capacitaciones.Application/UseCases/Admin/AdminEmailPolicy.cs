namespace Capacitaciones.Application.UseCases.Admin;

/// <summary>
/// Política de email para administradores: debe ser un correo corporativo <c>@dos.com.ec</c>.
/// Alineada con la regla de inscripción pública descrita en la sección 7.3 de <c>instrucciones.md</c>.
/// </summary>
public static class AdminEmailPolicy
{
    public const string RequiredDomain = "@dos.com.ec";

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var e = email.Trim();
        if (e.Length > 255) return false;

        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@')) return false;

        var local = e[..at];
        if (string.IsNullOrWhiteSpace(local)) return false;

        return e.EndsWith(RequiredDomain, StringComparison.OrdinalIgnoreCase);
    }
}
