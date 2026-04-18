namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Política centralizada del logo de capacitación (Fase 9): whitelist de extensiones,
/// MIME types permitidos y tamaño máximo. Consumida por <see cref="SubirLogoCapacitacionUseCase"/>
/// y por cualquier validador externo que necesite los mismos límites.
/// </summary>
public static class LogoCapacitacionPolicy
{
    /// <summary>Tamaño máximo en bytes (2 MB) — ver instrucciones §7.8.</summary>
    public const long MaxBytes = 2L * 1024 * 1024;

    /// <summary>Whitelist de extensiones, case-insensitive, sin el punto inicial.</summary>
    public static readonly IReadOnlySet<string> ExtensionesPermitidas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "png", "jpg", "jpeg", "webp", "svg"
        };

    /// <summary>MIME types aceptados, case-insensitive.</summary>
    public static readonly IReadOnlySet<string> ContentTypesPermitidos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/webp",
            "image/svg+xml"
        };

    /// <summary>
    /// Normaliza la extensión (sin punto, minúsculas) tomando la última en nombres con
    /// múltiples puntos. Devuelve null si no hay extensión.
    /// </summary>
    public static string? NormalizarExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var trimmed = fileName.Trim();
        var dot = trimmed.LastIndexOf('.');
        if (dot < 0 || dot == trimmed.Length - 1) return null;
        var ext = trimmed[(dot + 1)..].Trim();
        return ext.Length == 0 ? null : ext.ToLowerInvariant();
    }

    /// <summary>True si la extensión está en la whitelist.</summary>
    public static bool EsExtensionPermitida(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && ExtensionesPermitidas.Contains(extension!);

    /// <summary>
    /// True si el MIME declarado coincide con la whitelist. Se tolera el caso en que el
    /// cliente no envíe contentType (algunos navegadores lo omiten para SVG) — en ese caso
    /// el caller decide si aceptar.
    /// </summary>
    public static bool EsContentTypePermitido(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && ContentTypesPermitidos.Contains(contentType!);

    /// <summary>
    /// True si la combinación (extension, contentType) es coherente. Acepta pares:
    /// png/image-png, jpg|jpeg/image-jpeg, webp/image-webp, svg/image-svg+xml.
    /// </summary>
    public static bool ExtensionYContentTypeCoherentes(string extension, string contentType)
    {
        if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(contentType)) return false;
        var ext = extension.Trim().TrimStart('.').ToLowerInvariant();
        var ct = contentType.Trim().ToLowerInvariant();
        return ext switch
        {
            "png" => ct == "image/png",
            "jpg" or "jpeg" => ct == "image/jpeg",
            "webp" => ct == "image/webp",
            "svg" => ct == "image/svg+xml",
            _ => false
        };
    }
}
