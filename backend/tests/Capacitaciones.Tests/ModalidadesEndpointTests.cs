using System.Net;

namespace Capacitaciones.Tests;

/// <summary>
/// Test de humo: el endpoint GET /api/catalogos/modalidades responde 200 OK
/// cuando la llamada lleva un token JWT válido (Fase 2 protege el endpoint con policy "Admin").
/// </summary>
public class ModalidadesEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    public ModalidadesEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetModalidades_AutenticadoComoAdmin_ReturnsOk()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/catalogos/modalidades");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotNull(body);
        // La respuesta puede ser "[]" si InMemory no aplica seeds; lo importante es que no falla.
    }
}
