using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Capacitaciones.Api.Filters;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>Factory que fija llave de cifrado, API key interna y un mail_sender falso.</summary>
public class CorreoWebAppFactory : InMemoryWebAppFactory
{
    public const string InternalKey = "test-internal-key";
    internal FakeMailSenderClient Mail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ISecretProtector>(
                new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
            services.AddSingleton(new InternalApiOptions { ApiKey = InternalKey });
            services.AddSingleton<IMailSenderClient>(Mail);
        });
    }

    /// <summary>
    /// Crea un HttpClient autenticado como el admin sembrado, generando el JWT directamente vía
    /// IJwtTokenGenerator (en vez de pasar por /api/auth/login, que ahora valida contra Active
    /// Directory y no sirve en este entorno de pruebas).
    /// </summary>
    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        var generator = Services.GetRequiredService<IJwtTokenGenerator>();
        var result = generator.Generate(new AdminUser
        {
            Id = SeededAdminId,
            Email = SeededAdminEmail,
            Nombres = "Test Admin",
            Activo = true
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        return client;
    }
}

public class CorreoEndpointsTests : IClassFixture<CorreoWebAppFactory>
{
    private readonly CorreoWebAppFactory _factory;

    public CorreoEndpointsTests(CorreoWebAppFactory factory)
    {
        _factory = factory;
    }

    private static object ValidPayload(string? password = "Clave123*") => new
    {
        smtpHost = "smtp.office365.com",
        smtpPort = 587,
        password,
        usarTls = true,
        remitenteCorreo = "capacitaciones@dos.com.ec",
        remitenteNombre = "CapacitaDOS",
        ccGlobal = "copia@dos.com.ec"
    };

    [Fact]
    public async Task Correo_SinToken_401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/configuracion/correo");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Correo_PutYGet_NoExponePassword()
    {
        var client = _factory.CreateAdminClient();

        var put = await client.PutAsJsonAsync("/api/configuracion/correo", ValidPayload());
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var raw = await client.GetStringAsync("/api/configuracion/correo");
        Assert.DoesNotContain("Clave123*", raw);
        using var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.GetProperty("configurado").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("tienePassword").GetBoolean());
    }

    [Fact]
    public async Task Correo_PutInvalido_400ConErroresPorCampo()
    {
        var client = _factory.CreateAdminClient();
        var response = await client.PutAsJsonAsync("/api/configuracion/correo", new
        {
            smtpHost = "", smtpPort = 587, remitenteCorreo = "x"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDACION", doc.RootElement.GetProperty("error").GetString());
        Assert.True(doc.RootElement.GetProperty("errores").TryGetProperty("SmtpHost", out _));
    }

    [Fact]
    public async Task Notificaciones_PutYGet()
    {
        var client = _factory.CreateAdminClient();
        var put = await client.PutAsJsonAsync("/api/configuracion/correo/notificaciones", new[]
        {
            new { plantilla = "encuesta_satisfaccion", activo = false, asuntoPersonalizado = (string?)null }
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/api/configuracion/correo/notificaciones"));
        var encuesta = doc.RootElement.EnumerateArray()
            .Single(e => e.GetProperty("plantilla").GetString() == "encuesta_satisfaccion");
        Assert.False(encuesta.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Prueba_UsaPasswordGuardadaYEnviaAlAdmin()
    {
        var client = _factory.CreateAdminClient();
        await client.PutAsJsonAsync("/api/configuracion/correo", ValidPayload("Guardada1*"));

        var response = await client.PostAsJsonAsync("/api/configuracion/correo/prueba", ValidPayload(password: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Guardada1*", _factory.Mail.LastTest!.Smtp.Password);
        Assert.Equal(InMemoryWebAppFactory.SeededAdminEmail, _factory.Mail.LastTest.Recipient);
    }

    [Fact]
    public async Task Interna_SinHeader_403()
    {
        var response = await _factory.CreateClient().GetAsync("/api/internal/correo-config");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Interna_HeaderIncorrecto_403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Key", "otra");
        var response = await client.GetAsync("/api/internal/correo-config");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Interna_HeaderCorrecto_DevuelveNotificaciones()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Internal-Key", CorreoWebAppFactory.InternalKey);

        var response = await client.GetAsync("/api/internal/correo-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("notificaciones").TryGetProperty("certificado_participante", out _));
    }
}
