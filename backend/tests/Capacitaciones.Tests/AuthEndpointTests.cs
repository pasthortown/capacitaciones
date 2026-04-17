using System.Net;
using System.Net.Http.Json;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests de autenticación de Fase 2:
///   1) login con credenciales inválidas -> 401.
///   2) login con el admin sembrado -> 200 + token.
///   3) GET /api/catalogos/modalidades sin token -> 401.
///   4) GET /api/catalogos/modalidades con token válido -> 200.
/// </summary>
public class AuthEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    public AuthEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_CredencialesInvalidas_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "desconocido@dos.com.ec",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_CredencialesValidas_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = InMemoryWebAppFactory.SeededAdminEmail,
            password = InMemoryWebAppFactory.SeededAdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
        Assert.True(payload.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(payload.User);
        Assert.Equal(InMemoryWebAppFactory.SeededAdminEmail, payload.User!.Email);
    }

    [Fact]
    public async Task GetModalidades_SinToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/catalogos/modalidades");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetModalidades_ConTokenValido_Returns200()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/catalogos/modalidades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserPayload? User { get; set; }
    }

    private sealed class UserPayload
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
    }
}
