using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Colaboradores;
using Capacitaciones.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Cliente REST del API de ControlTareas (SGI). Se autentica con un usuario de servicio vía
/// <c>POST /auth/login</c> y cachea el JWT (con expiración) en memoria del proceso para no
/// loguear en cada consulta. Best-effort: si la integración está deshabilitada o ControlTareas
/// falla, el listado devuelve vacío (no rompe RegistroCapacitaciones).
/// </summary>
public sealed class ControlTareasHttpClient : IControlTareasColaboradoresClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Cache de token a nivel de proceso (el typed client es transient, por eso es estático).
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? _token;
    private static DateTime _tokenExpiraUtc = DateTime.MinValue;

    private readonly HttpClient _http;
    private readonly ControlTareasOptions _o;
    private readonly ILogger<ControlTareasHttpClient> _log;

    public ControlTareasHttpClient(HttpClient http, ControlTareasOptions options, ILogger<ControlTareasHttpClient> log)
    {
        _http = http;
        _o = options;
        _log = log;
    }

    public bool Enabled => _o.Enabled;

    public async Task<IReadOnlyList<EmpleadoDosDto>> ListarAsync(string? buscar, bool incluirInactivos, CancellationToken ct = default)
    {
        if (!_o.Enabled) return Array.Empty<EmpleadoDosDto>();
        try
        {
            var token = await ObtenerTokenAsync(ct);
            if (token is null) return Array.Empty<EmpleadoDosDto>();

            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(buscar)) qs.Add($"buscar={Uri.EscapeDataString(buscar.Trim())}");
            if (incluirInactivos) qs.Add("incluirInactivos=true");
            var url = "empleados" + (qs.Count > 0 ? "?" + string.Join("&", qs) : string.Empty);

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("ControlTareas: GET /empleados respondió {Code}.", (int)resp.StatusCode);
                return Array.Empty<EmpleadoDosDto>();
            }
            var data = await resp.Content.ReadFromJsonAsync<List<EmpleadoDosDto>>(Json, ct);
            return data ?? new List<EmpleadoDosDto>();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "ControlTareas: no se pudo listar empleados.");
            return Array.Empty<EmpleadoDosDto>();
        }
    }

    public async Task<bool> ExisteCedulaAsync(string cedula, CancellationToken ct = default)
    {
        if (!_o.Enabled || string.IsNullOrWhiteSpace(cedula)) return false;
        var token = await ObtenerTokenAsync(ct);
        if (token is null) return false;

        using var req = new HttpRequestMessage(HttpMethod.Get, $"empleados/cedula/{Uri.EscapeDataString(cedula.Trim())}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return false;
        if (resp.IsSuccessStatusCode) return true;

        // Ante un error del API, no podemos garantizar la regla → fallamos seguro (no se puede crear).
        _log.LogWarning("ControlTareas: verificación de cédula respondió {Code}.", (int)resp.StatusCode);
        throw new InvalidOperationException(
            $"No se pudo verificar la cédula contra ControlTareas (HTTP {(int)resp.StatusCode}).");
    }

    /// <summary>Devuelve un JWT válido del usuario de servicio, reusando el cacheado si no expiró.</summary>
    private async Task<string?> ObtenerTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenExpiraUtc) return _token;

        await TokenLock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow < _tokenExpiraUtc) return _token;

            using var resp = await _http.PostAsJsonAsync("auth/login",
                new { username = _o.User, password = _o.Password }, Json, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("ControlTareas: login del usuario de servicio respondió {Code}.", (int)resp.StatusCode);
                return null;
            }

            var login = await resp.Content.ReadFromJsonAsync<LoginResponse>(Json, ct);
            if (login is null || string.IsNullOrWhiteSpace(login.Token)) return null;

            _token = login.Token;
            // Renovamos 5 min antes de la expiración real (o 30 min por defecto si no vino).
            var exp = login.ExpiresAt ?? DateTime.UtcNow.AddMinutes(35);
            _tokenExpiraUtc = exp.ToUniversalTime().AddMinutes(-5);
            return _token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private sealed class LoginResponse
    {
        public string? Token { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
