using Capacitaciones.Application.Dtos.Auth;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Auth;

/// <summary>
/// Caso de uso de login administrativo: valida credenciales y emite un JWT.
/// </summary>
public class LoginUseCase
{
    private readonly IAdminUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public LoginUseCase(IAdminUserRepository users, IPasswordHasher hasher, IJwtTokenGenerator jwt)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
    }

    /// <summary>
    /// Devuelve el token + datos de usuario si las credenciales son válidas.
    /// Retorna <c>null</c> si el email no existe, el usuario está inactivo o la contraseña es incorrecta.
    /// Se evita distinguir cada caso para no dar pistas a un atacante (misma respuesta 401 en el controller).
    /// </summary>
    public async Task<LoginResponseDto?> ExecuteAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var email = request.Email.Trim();
        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !user.Activo)
        {
            return null;
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

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
                Email = user.Email,
                Nombres = user.Nombres,
                Roles = new[] { "Admin" }
            }
        };
    }
}
