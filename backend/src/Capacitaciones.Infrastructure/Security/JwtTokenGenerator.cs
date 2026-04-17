using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Capacitaciones.Infrastructure.Security;

/// <summary>
/// Adaptador de <see cref="IJwtTokenGenerator"/> que emite tokens JWT firmados con HMAC-SHA256.
/// Claims emitidos: <c>sub</c> (id), <c>email</c>, <c>name</c> (nombres) y <c>role</c>=Admin.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public JwtTokenResult Generate(AdminUser user)
    {
        if (user is null) throw new ArgumentNullException(nameof(user));

        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            throw new InvalidOperationException("Jwt:Secret no está configurado.");
        }

        var now = DateTime.UtcNow;
        var expires = now.AddHours(_options.ExpirationHours > 0 ? _options.ExpirationHours : 8);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Nombres),
            new(ClaimTypes.Role, "Admin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(_options.Issuer) ? null : _options.Issuer,
            audience: string.IsNullOrWhiteSpace(_options.Audience) ? null : _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtTokenResult
        {
            Token = tokenString,
            ExpiresAt = expires
        };
    }

    /// <summary>
    /// Emite un JWT de capacitador para una capacitación. Firma con el mismo secret/issuer/audience
    /// que el token de admin — la policy "Capacitador" solo distingue por el claim <c>role</c>.
    /// </summary>
    public JwtTokenResult GenerateCapacitadorToken(Guid capacitacionId)
    {
        if (capacitacionId == Guid.Empty)
            throw new ArgumentException("capacitacionId requerido", nameof(capacitacionId));

        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            throw new InvalidOperationException("Jwt:Secret no está configurado.");
        }

        var now = DateTime.UtcNow;
        var dias = _options.CapacitadorTokenDias > 0 ? _options.CapacitadorTokenDias : 90;
        var expires = now.AddDays(dias);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var cid = capacitacionId.ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, cid),
            new(ClaimTypes.Role, "Capacitador"),
            new("scope", "capacitador"),
            new("cid", cid),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(_options.Issuer) ? null : _options.Issuer,
            audience: string.IsNullOrWhiteSpace(_options.Audience) ? null : _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtTokenResult
        {
            Token = tokenString,
            ExpiresAt = expires
        };
    }
}
