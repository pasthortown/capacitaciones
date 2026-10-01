using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Capacitaciones.Api.Filters;

/// <summary>API key compartida con servicios internos (mail_sender). Env: <c>MAIL_CONFIG_API_KEY</c>.</summary>
public class InternalApiOptions
{
    public string? ApiKey { get; set; }
}

/// <summary>
/// Exige el header <c>X-Internal-Key</c> igual a <see cref="InternalApiOptions.ApiKey"/>.
/// Sin llave configurada responde siempre 403. Además nginx bloquea /api/internal/ desde fuera.
/// </summary>
public class InternalApiKeyFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Internal-Key";

    private readonly InternalApiOptions _options;

    public InternalApiKeyFilter(InternalApiOptions options)
    {
        _options = options;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = _options.ApiKey;
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(expected)
            || string.IsNullOrEmpty(provided)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        }
    }
}
