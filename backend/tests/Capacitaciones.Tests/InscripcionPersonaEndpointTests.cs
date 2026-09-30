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
/// Integración del autocompletado por cédula y el reuso de firma en la inscripción pública:
///   - GET /api/inscripcion/capacitacion/persona/{id} → 200 sin firma / 404 / 401.
///   - POST /api/inscripcion/capacitacion con usarFirmaRegistrada → 201 y copia la firma guardada.
/// </summary>
public class InscripcionPersonaEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    private static readonly Guid SeededModalidadId = new("11111111-1111-1111-1111-111111110020");
    private static readonly Guid SeededTipoActividadId = new("22222222-2222-2222-2222-222222220020");
    private static readonly Guid SeededAreaId = new("44444444-4444-4444-4444-444444440020");
    private const string FirmaGuardada = "data:image/png;base64,GUARDADA==";

    public InscripcionPersonaEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
        EnsureSeeded();
    }

    private void EnsureSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (!db.Modalidades.Any(m => m.Id == SeededModalidadId))
            db.Modalidades.Add(new Modalidad { Id = SeededModalidadId, Nombre = "Presencial", Activo = true, FechaCreacion = t0 });
        if (!db.TiposActividad.Any(t => t.Id == SeededTipoActividadId))
            db.TiposActividad.Add(new TipoActividad { Id = SeededTipoActividadId, Nombre = "Charla", Activo = true, FechaCreacion = t0 });
        if (!db.Areas.Any(a => a.Id == SeededAreaId))
            db.Areas.Add(new Area { Id = SeededAreaId, Nombre = "Area persona", Activo = true, FechaCreacion = t0 });
        db.SaveChanges();
    }

    private Guid CrearCapacitacion()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cap = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = $"CAP-PC-REG-{Random.Shared.Next(100, 999):000}",
            Tema = "Persona inscrita test",
            Capacitador = "Ana",
            ModalidadId = SeededModalidadId,
            TipoActividadId = SeededTipoActividadId,
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

    private void CrearPersona(string identificacion)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PersonasInscritas.Add(new PersonaInscrita
        {
            Id = Guid.NewGuid(),
            Identificacion = identificacion,
            Nombres = "María Fernanda",
            Apellidos = "Pérez Torres",
            AreaId = SeededAreaId,
            EmailUsuario = "maria.perez@dos.com.ec",
            Firma = FirmaGuardada,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private HttpClient ClienteConToken(Guid capacitacionId)
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt.GenerateInscripcionToken(capacitacionId).Token);
        return client;
    }

    [Fact]
    public async Task BuscarPersona_Existente_Devuelve200SinFirma()
    {
        var capId = CrearCapacitacion();
        CrearPersona("1700000001");
        var client = ClienteConToken(capId);

        var resp = await client.GetAsync("/api/inscripcion/capacitacion/persona/1700000001");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("María Fernanda", root.GetProperty("nombres").GetString());
        Assert.Equal("Pérez Torres", root.GetProperty("apellidos").GetString());
        Assert.Equal("maria.perez", root.GetProperty("emailUsuario").GetString());
        Assert.Equal(SeededAreaId, root.GetProperty("areaId").GetGuid());
        Assert.True(root.GetProperty("tieneFirma").GetBoolean());
        Assert.False(root.TryGetProperty("firma", out _));
        Assert.DoesNotContain("GUARDADA", root.GetRawText());
    }

    [Fact]
    public async Task BuscarPersona_Inexistente_Devuelve404()
    {
        var client = ClienteConToken(CrearCapacitacion());

        var resp = await client.GetAsync("/api/inscripcion/capacitacion/persona/0999999999");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task BuscarPersona_SinToken_Devuelve401()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/inscripcion/capacitacion/persona/1700000001");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Inscribir_UsandoFirmaRegistrada_Devuelve201YCopiaLaFirma()
    {
        var capId = CrearCapacitacion();
        CrearPersona("1700000002");
        var client = ClienteConToken(capId);

        var resp = await client.PostAsJsonAsync("/api/inscripcion/capacitacion", new
        {
            nombres = "María Fernanda",
            apellidos = "Pérez Torres",
            identificacion = "1700000002",
            areaId = SeededAreaId,
            emailUsuario = "maria.perez",
            firma = (string?)null,
            usarFirmaRegistrada = true
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asistente = await db.Asistentes.AsNoTracking().SingleAsync(a => a.CapacitacionId == capId && a.Identificacion == "1700000002");
        Assert.Equal(FirmaGuardada, asistente.Firma);
    }

    [Fact]
    public async Task Inscribir_PrimeraVez_CreaPersona()
    {
        var capId = CrearCapacitacion();
        var client = ClienteConToken(capId);

        var resp = await client.PostAsJsonAsync("/api/inscripcion/capacitacion", new
        {
            nombres = "Juan",
            apellidos = "Perez",
            identificacion = "1700000003",
            areaId = SeededAreaId,
            emailUsuario = "juan.perez",
            firma = "data:image/png;base64,NUEVA==",
            usarFirmaRegistrada = false
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persona = await db.PersonasInscritas.AsNoTracking().SingleAsync(p => p.Identificacion == "1700000003");
        Assert.Equal("data:image/png;base64,NUEVA==", persona.Firma);
        Assert.Equal("juan.perez@dos.com.ec", persona.EmailUsuario);
    }

    [Fact]
    public async Task Inscribir_UsarFirmaRegistradaSinPersona_Devuelve400ConCodigo()
    {
        var client = ClienteConToken(CrearCapacitacion());

        var resp = await client.PostAsJsonAsync("/api/inscripcion/capacitacion", new
        {
            nombres = "Ana",
            apellidos = "Lopez",
            identificacion = "1700000004",
            areaId = SeededAreaId,
            emailUsuario = "ana.lopez",
            usarFirmaRegistrada = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal("FIRMA_REGISTRADA_NO_DISPONIBLE", doc.RootElement.GetProperty("error").GetString());
    }
}
