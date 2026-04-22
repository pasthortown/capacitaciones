using System.Net.Mail;

namespace Capacitaciones.Application.UseCases.Responsables;

/// <summary>
/// Validaciones compartidas por los UseCases Crear/Editar de responsable (admin y perfil público).
/// </summary>
internal static class ResponsableValidator
{
    public const int CampoMaxLength = 255;
    public const int EmailMaxLength = 320;

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

    public static void ValidarEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ResponsableServiceException("INVALID_EMAIL", "'email' es requerido.");
        if (email.Length > EmailMaxLength)
            throw new ResponsableServiceException("INVALID_EMAIL", $"'email' excede el máximo de {EmailMaxLength} caracteres.");

        // MailAddress.TryCreate valida formato RFC5321-ish: local@dominio.
        if (!MailAddress.TryCreate(email.Trim(), out _))
            throw new ResponsableServiceException("INVALID_EMAIL", "'email' no tiene formato válido.");
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
