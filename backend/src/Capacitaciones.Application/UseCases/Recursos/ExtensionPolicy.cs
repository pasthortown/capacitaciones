namespace Capacitaciones.Application.UseCases.Recursos;

/// <summary>
/// Política centralizada de extensiones prohibidas para el módulo Repositorio.
/// La blacklist es un guardarraíl básico contra binarios ejecutables y scripts que
/// podrían ejecutarse al descargarse. NO reemplaza el escaneo AV (fuera de alcance v1).
///
/// Comparación: case-insensitive, sin el punto inicial. Archivos sin extensión se aceptan.
/// </summary>
public static class ExtensionPolicy
{
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        // Ejecutables
        "exe", "msi", "com", "scr", "dll", "bat", "cmd", "bin",
        "apk", "app", "dmg", "deb", "rpm", "jar", "war",
        // Scripts
        "sh", "bash", "zsh", "ksh",
        "ps1", "psm1", "psd1",
        "vbs", "vbe", "wsf", "wsh",
        "js", "jse", "mjs", "cjs", "ts",
        "py", "pyc", "pyw",
        "rb", "pl", "php", "phtml",
        "reg", "lnk", "htaccess"
    };

    /// <summary>
    /// True si la extensión (o su ausencia, null) está permitida.
    /// Se compara insensible a mayúsculas; "EXE", "exe", ".exe" son equivalentes.
    /// </summary>
    public static bool IsAllowed(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return true;
        var norm = extension.Trim().TrimStart('.').ToLowerInvariant();
        return !Blocked.Contains(norm);
    }

    /// <summary>
    /// Devuelve la extensión en minúsculas, sin el punto, considerando solo la última
    /// en nombres con múltiples puntos (ej: <c>backup.tar.gz</c> → <c>gz</c>).
    /// Si el archivo no tiene extensión (o termina con un punto vacío), devuelve null.
    /// </summary>
    public static string? Normalize(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;
        var trimmed = filename.Trim();
        var dot = trimmed.LastIndexOf('.');
        if (dot < 0 || dot == trimmed.Length - 1) return null;
        var ext = trimmed[(dot + 1)..].Trim();
        return ext.Length == 0 ? null : ext.ToLowerInvariant();
    }
}
