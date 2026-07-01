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
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.Nombres ?? string.Empty),
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
        var horas = _options.CapacitadorTokenHoras > 0 ? _options.CapacitadorTokenHoras : 48;
        return GenerateResourceToken(capacitacionId, role: "Capacitador", scope: "capacitador", idClaim: "cid", horas: horas);
    }

    /// <summary>
    /// Emite un JWT para el link público de inscripción (Fase 5). Firma con el mismo secret/issuer/audience;
    /// la policy "Inscripcion" distingue por el claim <c>role</c>.
    /// </summary>
    public JwtTokenResult GenerateInscripcionToken(Guid capacitacionId)
    {
        var horas = _options.InscripcionTokenHoras > 0 ? _options.InscripcionTokenHoras : 48;
        return GenerateResourceToken(capacitacionId, role: "Inscripcion", scope: "inscripcion", idClaim: "cid", horas: horas);
    }

    /// <summary>
    /// Emite un JWT para el link público del responsable. Firma con el mismo secret/issuer/audience;
    /// la policy "Responsable" distingue por el claim <c>role</c>. El id del responsable viaja en <c>rid</c>
    /// para no chocar con el <c>cid</c> que usan los tokens de capacitación.
    /// </summary>
    public JwtTokenResult GenerateResponsableToken(Guid responsableId)
    {
        var horas = _options.ResponsableTokenHoras > 0 ? _options.ResponsableTokenHoras : 48;
        return GenerateResourceToken(responsableId, role: "Responsable", scope: "responsable", idClaim: "rid", horas: horas);
    }

    /// <summary>
    /// Emite un JWT para el link de pase de lista del capacitador (Fase 10).
    /// Reutiliza el claim <c>cid</c> que ya usan los tokens de capacitación para que el controller
    /// pueda compartir el helper <c>TryGetCapacitacionId</c>. La policy "PaseLista" distingue por role.
    /// </summary>
    public JwtTokenResult GeneratePaseListaToken(Guid capacitacionId)
    {
        var horas = _options.PaseListaTokenHoras > 0 ? _options.PaseListaTokenHoras : 48;
        return GenerateResourceToken(capacitacionId, role: "PaseLista", scope: "pase-lista", idClaim: "cid", horas: horas);
    }

    /// <summary>
    /// Emite un JWT para el link de calificaciones del capacitador (Fase 11). Igual que el token
    /// de pase de lista pero con role/scope propios — la policy "Calificaciones" lo distingue.
    /// </summary>
    public JwtTokenResult GenerateCalificacionesToken(Guid capacitacionId)
    {
        var horas = _options.CalificacionesTokenHoras > 0 ? _options.CalificacionesTokenHoras : 48;
        return GenerateResourceToken(capacitacionId, role: "Calificaciones", scope: "calificaciones", idClaim: "cid", horas: horas);
    }

    /// <summary>
    /// Helper compartido por los tokens de recursos públicos (capacitador/inscripción/responsable):
    /// firman un recurso (<paramref name="resourceId"/>) con los claims <c>sub</c> + <c>role</c> +
    /// <c>scope</c> + un claim específico (<paramref name="idClaim"/> = "cid" o "rid") que replica el id.
    /// </summary>
    private JwtTokenResult GenerateResourceToken(Guid resourceId, string role, string scope, string idClaim, int horas)
    {
        if (resourceId == Guid.Empty)
            throw new ArgumentException("resourceId requerido", nameof(resourceId));

        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            throw new InvalidOperationException("Jwt:Secret no está configurado.");
        }

        var now = DateTime.UtcNow;
        // Requerimiento: los enlaces (capacitador/inscripción/responsable/pase-lista/calificaciones)
        // NO deben caducar. Se emiten sin claim `exp`, por lo que la validación de lifetime del
        // backend no los rechaza (ver RequireExpirationTime=false en Program.cs). El parámetro
        // `horas` se conserva por compatibilidad de firma pero ya no limita la vigencia.
        _ = horas;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var id = resourceId.ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(ClaimTypes.Role, role),
            new("scope", scope),
            new(idClaim, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(_options.Issuer) ? null : _options.Issuer,
            audience: string.IsNullOrWhiteSpace(_options.Audience) ? null : _options.Audience,
            claims: claims,
            notBefore: now,
            expires: null,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtTokenResult
        {
            Token = tokenString,
            // Sin caducidad real; se reporta DateTime.MaxValue para señalar "no expira" al frontend.
            ExpiresAt = DateTime.MaxValue
        };
    }
}
