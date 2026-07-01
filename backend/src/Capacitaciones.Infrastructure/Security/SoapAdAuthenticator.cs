using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Xml.Linq;
using Capacitaciones.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Capacitaciones.Infrastructure.Security;

/// <summary>Configuración de Active Directory (por entorno). <c>AuthUrl</c> = .../CMDBWS/Authentication.asmx,
/// <c>Domain</c> = COMPUEQUIP. Sin <c>AuthUrl</c> el login por dominio queda deshabilitado.</summary>
public sealed class AdOptions
{
    public string? AuthUrl { get; set; }
    public string? Domain { get; set; }
}

/// <summary>
/// Autenticación contra AD vía el SOAP <c>AutenticateUserAD</c> del portal de servicios (réplica del
/// legacy usado por ControlTareas). Valida la credencial y devuelve Login/Name/Email. Config-gated.
/// </summary>
public sealed class SoapAdAuthenticator : IAdAuthenticator
{
    private const string Ns = "http://ArandaCMDBService.org/";
    private readonly HttpClient _http;
    private readonly AdOptions _o;
    private readonly ILogger<SoapAdAuthenticator> _log;

    public SoapAdAuthenticator(HttpClient http, AdOptions o, ILogger<SoapAdAuthenticator> log)
    {
        _http = http;
        _o = o;
        _log = log;
    }

    public bool Enabled => !string.IsNullOrWhiteSpace(_o.AuthUrl);

    public async Task<AdUser?> ValidateAsync(string username, string password, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var soap =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<soap:Envelope xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:t=\"" + Ns + "\">" +
            "<soap:Body><t:AutenticateUserAD>" +
            "<t:userName>" + SecurityElement.Escape(username.Trim()) + "</t:userName>" +
            "<t:password>" + SecurityElement.Escape(password) + "</t:password>" +
            "<t:domain>" + SecurityElement.Escape(_o.Domain ?? string.Empty) + "</t:domain>" +
            "</t:AutenticateUserAD></soap:Body></soap:Envelope>";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _o.AuthUrl);
            req.Content = new StringContent(soap, Encoding.UTF8, "text/xml");
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
            req.Headers.Add("SOAPAction", "\"" + Ns + "AutenticateUserAD\"");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("AD SOAP: respuesta {Status} al validar {User}.", (int)resp.StatusCode, username);
                return null;
            }

            var doc = XDocument.Parse(body);
            var result = doc.Descendants(XName.Get("AutenticateUserADResult", Ns)).FirstOrDefault();
            if (result is null) return null; // sin resultado → credencial inválida

            var active = int.TryParse(El(result, "ActiveUser"), out var a) ? a : -1;
            if (active != 0) return null; // legacy: ActiveUser == 0 → válida/activa

            var login = El(result, "Login") ?? username.Trim();
            var name = El(result, "Name") ?? login;
            var email = El(result, "Email");
            return new AdUser(login, name, string.IsNullOrWhiteSpace(email) ? null : email);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "AD SOAP: error al validar credenciales de {User}.", username);
            return null;
        }
    }

    private static string? El(XElement parent, string name) => parent.Element(XName.Get(name, Ns))?.Value;
}
