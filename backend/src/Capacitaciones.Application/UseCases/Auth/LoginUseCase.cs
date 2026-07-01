using Capacitaciones.Application.Dtos.Auth;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Auth;

/// <summary>
/// Caso de uso de login administrativo: valida credenciales y emite un JWT.
/// </summary>
public class LoginUseCase
{
    private readonly IAdminUserRepository _users;
    private readonly IJwtTokenGenerator _jwt;
    private readonly IAdAuthenticator _ad;

    public LoginUseCase(IAdminUserRepository users, IJwtTokenGenerator jwt, IAdAuthenticator ad)
    {
        _users = users;
        _jwt = jwt;
        _ad = ad;
    }

    /// <summary>
    /// Login <b>solo por dominio</b>: valida usuario de red + contraseña contra el AD (SOAP del portal)
    /// y exige que el usuario de red esté en la <b>lista de permitidos</b> (AdminUser). El nombre para
    /// mostrar se toma del AD. Devuelve <c>null</c> (→ 401) si el dominio no valida o no está permitido;
    /// no se distinguen los casos para no dar pistas.
    /// </summary>
    public async Task<LoginResponseDto?> ExecuteAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var identificador = request?.Identificador ?? string.Empty;
        if (string.IsNullOrWhiteSpace(identificador) || string.IsNullOrWhiteSpace(request!.Password))
            return null;

        // Solo dominio: si el AD no está configurado, no se puede autenticar.
        if (!_ad.Enabled) return null;

        var adUser = await _ad.ValidateAsync(identificador, request.Password, ct);
        if (adUser is null) return null; // credencial de dominio inválida

        var login = string.IsNullOrWhiteSpace(adUser.Login) ? identificador : adUser.Login;

        // Gate: el usuario de red debe estar en la lista de permitidos y activo.
        var user = await _users.GetByUsuarioRedAsync(login, ct)
                   ?? await _users.GetByUsuarioRedAsync(identificador, ct);
        if (user is null || !user.Activo) return null; // no autorizado

        // Refresca datos de presentación desde el AD.
        if (!string.IsNullOrWhiteSpace(adUser.Name)) user.Nombres = adUser.Name;
        if (!string.IsNullOrWhiteSpace(adUser.Email)) user.Email = adUser.Email!;
        user.UltimoLogin = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);

        var token = _jwt.Generate(user);
        return new LoginResponseDto
        {
            Token = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Nombres = string.IsNullOrWhiteSpace(user.Nombres) ? login : user.Nombres,
                Roles = new[] { "Admin" }
            }
        };
    }
}
