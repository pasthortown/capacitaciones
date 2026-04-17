namespace Capacitaciones.Application.Dtos.Auth;

/// <summary>
/// Datos del usuario autenticado devueltos en <c>/api/auth/login</c> y <c>/api/auth/me</c>.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;

    /// <summary>
    /// Lista de roles asociados. En esta versión siempre contiene <c>["Admin"]</c>.
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
