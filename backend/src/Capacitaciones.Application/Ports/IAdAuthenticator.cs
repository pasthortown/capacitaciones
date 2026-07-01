namespace Capacitaciones.Application.Ports;

/// <summary>Datos básicos que el dominio (AD) devuelve al validar una credencial.</summary>
public record AdUser(string Login, string Name, string? Email);

/// <summary>
/// Valida credenciales contra el dominio corporativo (AD vía el SOAP del portal de servicios).
/// Config-gated: si no hay URL configurada, <see cref="Enabled"/> es <c>false</c>.
/// </summary>
public interface IAdAuthenticator
{
    bool Enabled { get; }
    Task<AdUser?> ValidateAsync(string username, string password, CancellationToken ct = default);
}
