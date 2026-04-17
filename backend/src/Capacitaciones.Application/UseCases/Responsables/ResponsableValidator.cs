namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Validaciones compartidas por los UseCases Crear/Editar de responsable (admin y perfil público).
/// </summary>
internal static class ResponsableValidator
{
    public const int CampoMaxLength = 255;

    public static void ValidarNombres(string? nombres)
    {
        if (string.IsNullOrWhiteSpace(nombres))
            throw new ResponsableServiceException("INVALID_NOMBRES", "'nombres' es requerido.");
        if (nombres.Length > CampoMaxLength)
            throw new ResponsableServiceException("INVALID_NOMBRES", $"'nombres' excede el máximo de {CampoMaxLength} caracteres.");
    }

    public static void ValidarCargo(string? cargo)
    {
        if (string.IsNullOrWhiteSpace(cargo))
            throw new ResponsableServiceException("INVALID_CARGO", "'cargo' es requerido.");
        if (cargo.Length > CampoMaxLength)
            throw new ResponsableServiceException("INVALID_CARGO", $"'cargo' excede el máximo de {CampoMaxLength} caracteres.");
    }

    public static void ValidarEmpresa(string? empresa)
    {
        if (string.IsNullOrWhiteSpace(empresa))
            throw new ResponsableServiceException("INVALID_EMPRESA", "'empresa' es requerido.");
        if (empresa.Length > CampoMaxLength)
            throw new ResponsableServiceException("INVALID_EMPRESA", $"'empresa' excede el máximo de {CampoMaxLength} caracteres.");
    }

    /// <summary>
    /// Normaliza a string? haciendo trim. Si queda vacío o era null, devuelve null.
    /// Útil para cargos/empresas/firmas opcionales donde vacío == sin valor.
    /// </summary>
    public static string? TrimToNull(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
