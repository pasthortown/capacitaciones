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
/// Tests de integración del módulo Fase 10 (pase de lista). Cubren:
///   - admin <c>POST /api/capacitaciones/{id}/link-pase-lista</c> → token válido.
///   - público <c>GET /api/capacitador/pase-lista</c> con token PaseLista → 200.
///   - público <c>PUT /api/capacitador/pase-lista/asistentes/{id}</c> → 200.
///   - público sin token o con token de otra policy (Capacitador) → 401/403.
///   - público PUT sobre asistente de otra capacitación → 404.
///   - admin <c>PUT /api/capacitaciones/{id}/asistentes/{id}/asistencia</c>.
/// </summary>
public class PaseListaEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    private static readonly Guid SeededModalidadId = new("11111111-1111-1111-1111-111111110010");
    private static readonly Guid SeededTipoActividadId = new("22222222-2222-2222-2222-222222220010");
    private static readonly Guid SeededAreaId = new("44444444-4444-4444-4444-444444440010");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PaseListaEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
        EnsureSeeded(_factory);
    }

    private static void EnsureSeeded(InMemoryWebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Modalidades.Any(m => m.Id == SeededModalidadId))
        {
            db.Modalidades.Add(new Modalidad
            {
                Id = SeededModalidadId,
                Nombre = "Presencial",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.TiposActividad.Any(t => t.Id == SeededTipoActividadId))
        {
            db.TiposActividad.Add(new TipoActividad
            {
                Id = SeededTipoActividadId,
                Nombre = "Charla",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.Areas.Any(a => a.Id == SeededAreaId))
        {
            db.Areas.Add(new Area
            {
                Id = SeededAreaId,
                Nombre = "Area de prueba",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        db.SaveChanges();
    }

    private static Guid CreateCapacitacionDirect(InMemoryWebAppFactory factory, out Guid capacitacionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cap = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = $"CAP-PC-REG-{Random.Shared.Next(100, 999):000}",
            Tema = "Pase de lista test",
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
        capacitacionId = cap.Id;
        return cap.Id;
    }

    private static Guid CreateAsistenteDirect(
        InMemoryWebAppFactory factory,
        Guid capacitacionId,
        string nombres = "Juan",
        string apellidos = "Perez",
        string identificacion = "1712345678")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asis = new Asistente
        {
            Id = Guid.NewGuid(),
            CapacitacionId = capacitacionId,
            Nombres = nombres,
            Apellidos = apellidos,
            Identificacion = identificacion,
            AreaId = SeededAreaId,
            EmailUsuario = $"{nombres}.{apellidos}@dos.com.ec",
            Firma = "data:image/png;base64,AAA==",
            FechaInscripcion = DateTime.UtcNow
        };
        db.Asistentes.Add(asis);
        db.SaveChanges();
        return asis.Id;
    }

    private static string GeneratePaseListaToken(InMemoryWebAppFactory factory, Guid capacitacionId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        return jwt.GeneratePaseListaToken(capacitacionId).Token;
    }

    private static string GenerateCapacitadorToken(InMemoryWebAppFactory factory, Guid capacitacionId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        return jwt.GenerateCapacitadorToken(capacitacionId).Token;
    }

    [Fact]
    public async Task AdminGenerarLinkPaseLista_DevuelveTokenYUrl()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);

        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/capacitaciones/{capacitacionId}/link-pase-lista", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<LinkPayload>(JsonOpts);
        Assert.NotNull(dto);
        Assert.False(string.IsNullOrWhiteSpace(dto!.Token));
        Assert.Contains("/capacitador/pase-lista?token=", dto.Url);
    }

    [Fact]
    public async Task AdminGenerarLinkPaseLista_CapacitacionInexistente_Returns404()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/capacitaciones/{Guid.NewGuid()}/link-pase-lista", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaseListaGet_ConTokenValido_Returns200YOrdenaAlfabeticamente()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        CreateAsistenteDirect(factory, capacitacionId, "Maria", "Perez", "1001");
        CreateAsistenteDirect(factory, capacitacionId, "Ana", "Alvarez", "1002");
        CreateAsistenteDirect(factory, capacitacionId, "Juan", "Perez", "1003");

        var token = GeneratePaseListaToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/capacitador/pase-lista");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<PaseListaPayload>(JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal(capacitacionId, dto!.Capacitacion.Id);
        Assert.Equal(3, dto.Asistentes.Count);
        Assert.Equal("Alvarez", dto.Asistentes[0].Apellidos);
        Assert.Equal("Perez", dto.Asistentes[1].Apellidos);
        Assert.Equal("Juan", dto.Asistentes[1].Nombres);
        Assert.Equal("Maria", dto.Asistentes[2].Nombres);
    }

    [Fact]
    public async Task PaseListaGet_SinToken_Returns401()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/capacitador/pase-lista");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PaseListaGet_ConTokenCapacitador_Returns403()
    {
        // Un token de role=Capacitador NO debe poder llamar endpoints de role=PaseLista
        // (policies excluyentes). Esto valida la decisión 9 del documento maestro.
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);

        var token = GenerateCapacitadorToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/capacitador/pase-lista");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PaseListaPut_MarcaPresente_Returns200()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId);

        var token = GeneratePaseListaToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/capacitador/pase-lista/asistentes/{asistenteId}",
            new { estadoAsistencia = "Presente" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<MarcacionPayload>(JsonOpts);
        Assert.Equal("Presente", dto!.EstadoAsistencia);
        Assert.NotNull(dto.FechaMarcacionAsistencia);

        // Verificamos en BD — la fila quedó actualizada.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asis = await db.Asistentes.AsNoTracking().FirstAsync(a => a.Id == asistenteId);
        Assert.Equal(EstadoAsistencia.Presente, asis.EstadoAsistencia);
    }

    [Fact]
    public async Task PaseListaPut_AsistenteDeOtraCapacitacion_Returns404()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionA);
        CreateCapacitacionDirect(factory, out var capacitacionB);
        // Asistente B pertenece a la capacitación B.
        var asisEnB = CreateAsistenteDirect(factory, capacitacionB, "Otro", "Aitante", "9999");

        // Token con cid=capacitacionA intenta marcar asisEnB (de capacitacionB) → 404.
        var token = GeneratePaseListaToken(factory, capacitacionA);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/capacitador/pase-lista/asistentes/{asisEnB}",
            new { estadoAsistencia = "Presente" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PaseListaPut_EstadoInvalido_Returns400()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId);

        var token = GeneratePaseListaToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/capacitador/pase-lista/asistentes/{asistenteId}",
            new { estadoAsistencia = "Marciano" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminMarcarAsistencia_Returns200YActualizaEntidad()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId);

        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/asistencia",
            new { estadoAsistencia = "Ausente" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<MarcacionPayload>(JsonOpts);
        Assert.Equal("Ausente", dto!.EstadoAsistencia);
    }

    [Fact]
    public async Task AdminMarcarAsistencia_EstadoNull_LimpiaMarcacion()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId);

        // Primero lo marcamos presente.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = db.Asistentes.Single(x => x.Id == asistenteId);
            a.EstadoAsistencia = EstadoAsistencia.Presente;
            a.FechaMarcacionAsistencia = DateTime.UtcNow.AddHours(-1);
            db.SaveChanges();
        }

        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/asistencia",
            new { estadoAsistencia = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var a2 = db2.Asistentes.AsNoTracking().Single(x => x.Id == asistenteId);
        Assert.Null(a2.EstadoAsistencia);
        Assert.Null(a2.FechaMarcacionAsistencia);
    }

    // --- Payloads locales a los tests.
    private sealed class LinkPayload
    {
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    private sealed class PaseListaPayload
    {
        public CapacitacionPayload Capacitacion { get; set; } = new();
        public List<AsistentePayload> Asistentes { get; set; } = new();
    }

    private sealed class CapacitacionPayload
    {
        public Guid Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Tema { get; set; } = string.Empty;
        public DateTime FechaHoraInicio { get; set; }
        public int DuracionMinutos { get; set; }
        public string Estado { get; set; } = string.Empty;
    }

    private sealed class AsistentePayload
    {
        public Guid Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public string? EstadoAsistencia { get; set; }
        public DateTime? FechaMarcacionAsistencia { get; set; }
    }

    private sealed class MarcacionPayload
    {
        public Guid Id { get; set; }
        public string? EstadoAsistencia { get; set; }
        public DateTime? FechaMarcacionAsistencia { get; set; }
    }
}
