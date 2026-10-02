using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>
/// Integración de la pantalla admin "Inscritos" (<c>api/inscritos</c>). El token admin se emite
/// directo con <see cref="IJwtTokenGenerator"/> (el login de prueba depende de AD).
/// </summary>
public class InscritosEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    private static readonly Guid ModalidadId = new("11111111-1111-1111-1111-111111110030");
    private static readonly Guid TipoActividadId = new("22222222-2222-2222-2222-222222220030");
    private static readonly Guid AreaId = new("44444444-4444-4444-4444-444444440030");
    private const string Firma = "data:image/png;base64,FIRMAINSCRITO==";

    public InscritosEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (!db.Modalidades.Any(m => m.Id == ModalidadId))
            db.Modalidades.Add(new Modalidad { Id = ModalidadId, Nombre = "Presencial", Activo = true, FechaCreacion = t0 });
        if (!db.TiposActividad.Any(t => t.Id == TipoActividadId))
            db.TiposActividad.Add(new TipoActividad { Id = TipoActividadId, Nombre = "Charla", Activo = true, FechaCreacion = t0 });
        if (!db.Areas.Any(a => a.Id == AreaId))
            db.Areas.Add(new Area { Id = AreaId, Nombre = "Area inscritos", Activo = true, FechaCreacion = t0 });
        db.SaveChanges();
    }

    private Guid CrearCapacitacion(string tema)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cap = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = $"CAP-PC-REG-{Random.Shared.Next(100, 999):000}",
            Tema = tema,
            Capacitador = "Ana",
            ModalidadId = ModalidadId,
            TipoActividadId = TipoActividadId,
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = DateTime.UtcNow.AddDays(1),
            DuracionMinutos = 60,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        db.Capacitaciones.Add(cap);
        db.SaveChanges();
        return cap.Id;
    }

    private Guid CrearInscrito(Guid capacitacionId, string identificacion, string nombres = "Juan")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = new Asistente
        {
            Id = Guid.NewGuid(),
            CapacitacionId = capacitacionId,
            Nombres = nombres,
            Apellidos = "Perez",
            Identificacion = identificacion,
            AreaId = AreaId,
            EmailUsuario = "juan.perez@dos.com.ec",
            Firma = Firma,
            FechaInscripcion = DateTime.UtcNow
        };
        db.Asistentes.Add(a);
        db.SaveChanges();
        return a.Id;
    }

    private HttpClient AdminClient()
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var token = jwt.Generate(new AdminUser { Id = _factory.SeededAdminId, Email = InMemoryWebAppFactory.SeededAdminEmail, Nombres = "Admin", Activo = true }).Token;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Listar_FiltraPorCapacitacionYNoIncluyeFirma()
    {
        var capA = CrearCapacitacion("Tema A");
        var capB = CrearCapacitacion("Tema B");
        CrearInscrito(capA, "1800000001");
        CrearInscrito(capB, "1800000002");

        var resp = await AdminClient().GetAsync($"/api/inscritos?capacitacionId={capA}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("FIRMAINSCRITO", body);
        using var doc = JsonDocument.Parse(body);
        var item = Assert.Single(doc.RootElement.EnumerateArray());
        Assert.Equal("1800000001", item.GetProperty("identificacion").GetString());
        Assert.Equal("Tema A", item.GetProperty("capacitacionTema").GetString());
        Assert.Equal("Area inscritos", item.GetProperty("areaNombre").GetString());
        Assert.True(item.GetProperty("tieneFirma").GetBoolean());
        Assert.False(item.TryGetProperty("firma", out _));
    }

    [Fact]
    public async Task Listar_BuscaPorCedulaONombre()
    {
        var cap = CrearCapacitacion("Tema buscar");
        CrearInscrito(cap, "1800000011", "Zoila");
        CrearInscrito(cap, "1800000012", "Pedro");

        var porNombre = await AdminClient().GetFromJsonAsync<JsonElement>($"/api/inscritos?capacitacionId={cap}&buscar=zoil");
        var porCedula = await AdminClient().GetFromJsonAsync<JsonElement>($"/api/inscritos?buscar=1800000012");

        Assert.Equal("1800000011", Assert.Single(porNombre.EnumerateArray()).GetProperty("identificacion").GetString());
        Assert.Equal("Pedro", Assert.Single(porCedula.EnumerateArray()).GetProperty("nombres").GetString());
    }

    [Fact]
    public async Task Detalle_IncluyeFirmaYEmailLocal()
    {
        var id = CrearInscrito(CrearCapacitacion("Tema detalle"), "1800000021");

        var doc = await AdminClient().GetFromJsonAsync<JsonElement>($"/api/inscritos/{id}");

        Assert.Equal(Firma, doc.GetProperty("firma").GetString());
        Assert.Equal("juan.perez", doc.GetProperty("emailUsuario").GetString());
    }

    [Fact]
    public async Task Detalle_Inexistente_Devuelve404()
    {
        var resp = await AdminClient().GetAsync($"/api/inscritos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Editar_GuardaCambiosYActualizaRegistroDePersonas()
    {
        var id = CrearInscrito(CrearCapacitacion("Tema editar"), "1800000031", "Jaun");

        var resp = await AdminClient().PutAsJsonAsync($"/api/inscritos/{id}", new
        {
            nombres = "Juan",
            apellidos = "Pérez",
            identificacion = "1800000031",
            areaId = AreaId,
            emailUsuario = "juan.p",
            firma = "data:image/png;base64,CORREGIDA=="
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = await db.Asistentes.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal("Juan", a.Nombres);
        Assert.Equal("juan.p@dos.com.ec", a.EmailUsuario);
        Assert.Equal("data:image/png;base64,CORREGIDA==", a.Firma);
        var p = await db.PersonasInscritas.AsNoTracking().SingleAsync(x => x.Identificacion == "1800000031");
        Assert.Equal("Juan", p.Nombres);
        Assert.Equal("data:image/png;base64,CORREGIDA==", p.Firma);
    }

    [Fact]
    public async Task Editar_CedulaDuplicadaEnLaCapacitacion_Devuelve409()
    {
        var cap = CrearCapacitacion("Tema duplicado");
        CrearInscrito(cap, "1800000041");
        var id = CrearInscrito(cap, "1800000042");

        var resp = await AdminClient().PutAsJsonAsync($"/api/inscritos/{id}", new
        {
            nombres = "Juan",
            apellidos = "Perez",
            identificacion = "1800000041",
            areaId = AreaId,
            emailUsuario = "juan.perez"
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("INSCRIPCION_DUPLICADA", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SinToken_Devuelve401()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/inscritos");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
