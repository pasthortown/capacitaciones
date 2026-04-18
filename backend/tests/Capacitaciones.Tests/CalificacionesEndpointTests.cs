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
/// Tests de integración del módulo Fase 11 (calificaciones). Cubren:
///   - admin <c>POST /api/capacitaciones/{id}/link-calificaciones</c> → token válido.
///   - admin <c>POST /link-calificaciones</c> sobre capacitación de Participación → 409.
///   - público <c>GET /api/capacitador/calificaciones</c> con token Calificaciones → 200 + filtra a Presentes.
///   - público GET sin token o con token de otra policy → 401/403.
///   - público <c>PUT /api/capacitador/calificaciones/asistentes/{id}</c> sobre ausente → 409.
///   - público PUT con calificación fuera de rango → 400.
///   - público PUT sobre asistente de otra capacitación → 404.
///   - admin <c>PUT /api/capacitaciones/{id}/asistentes/{id}/calificacion</c> → 200.
/// </summary>
public class CalificacionesEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    private static readonly Guid SeededModalidadId = new("11111111-1111-1111-1111-111111110011");
    private static readonly Guid SeededTipoActividadId = new("22222222-2222-2222-2222-222222220011");
    private static readonly Guid SeededAreaId = new("44444444-4444-4444-4444-444444440011");

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public CalificacionesEndpointTests(InMemoryWebAppFactory factory)
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
                Nombre = "Virtual",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.TiposActividad.Any(t => t.Id == SeededTipoActividadId))
        {
            db.TiposActividad.Add(new TipoActividad
            {
                Id = SeededTipoActividadId,
                Nombre = "Curso",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.Areas.Any(a => a.Id == SeededAreaId))
        {
            db.Areas.Add(new Area
            {
                Id = SeededAreaId,
                Nombre = "Area calif",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        db.SaveChanges();
    }

    private static Guid CreateCapacitacionDirect(
        InMemoryWebAppFactory factory,
        out Guid capacitacionId,
        TipoCertificacion tipo = TipoCertificacion.Aprobacion,
        decimal? puntajeMinimo = 7.0m)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cap = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = $"CAP-PC-REG-{Random.Shared.Next(100, 999):000}",
            Tema = "Calificación test",
            Capacitador = "Ana",
            ModalidadId = SeededModalidadId,
            TipoActividadId = SeededTipoActividadId,
            TipoCertificacion = tipo,
            PuntajeMinimo = tipo == TipoCertificacion.Aprobacion ? puntajeMinimo : null,
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
        string identificacion = "1712345678",
        EstadoAsistencia? estadoAsistencia = EstadoAsistencia.Presente)
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
            FechaInscripcion = DateTime.UtcNow,
            EstadoAsistencia = estadoAsistencia,
            FechaMarcacionAsistencia = estadoAsistencia.HasValue ? DateTime.UtcNow.AddMinutes(-1) : null
        };
        db.Asistentes.Add(asis);
        db.SaveChanges();
        return asis.Id;
    }

    private static string GenerateCalificacionesToken(InMemoryWebAppFactory factory, Guid capacitacionId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        return jwt.GenerateCalificacionesToken(capacitacionId).Token;
    }

    private static string GenerateCapacitadorToken(InMemoryWebAppFactory factory, Guid capacitacionId)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        return jwt.GenerateCapacitadorToken(capacitacionId).Token;
    }

    [Fact]
    public async Task AdminGenerarLinkCalificaciones_DevuelveTokenYUrl()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);

        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/capacitaciones/{capacitacionId}/link-calificaciones", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<LinkPayload>(JsonOpts);
        Assert.NotNull(dto);
        Assert.False(string.IsNullOrWhiteSpace(dto!.Token));
        Assert.Contains("/capacitador/calificaciones?token=", dto.Url);
    }

    [Fact]
    public async Task AdminGenerarLinkCalificaciones_EnParticipacion_Returns409()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId, tipo: TipoCertificacion.Participacion);

        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/capacitaciones/{capacitacionId}/link-calificaciones", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorPayload>(JsonOpts);
        Assert.Equal("CALIFICACIONES_NO_APLICA", body!.Error);
    }

    [Fact]
    public async Task AdminGenerarLinkCalificaciones_CapacitacionInexistente_Returns404()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsync($"/api/capacitaciones/{Guid.NewGuid()}/link-calificaciones", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CalificacionesGet_ConTokenValido_FiltraPresentesYOrdena()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        CreateAsistenteDirect(factory, capacitacionId, "Maria", "Perez", "1001", EstadoAsistencia.Presente);
        CreateAsistenteDirect(factory, capacitacionId, "Ana", "Alvarez", "1002", EstadoAsistencia.Presente);
        CreateAsistenteDirect(factory, capacitacionId, "Pepe", "Valle", "1003", EstadoAsistencia.Ausente); // no viene
        CreateAsistenteDirect(factory, capacitacionId, "Neutro", "Nada", "1004", null); // no viene

        var token = GenerateCalificacionesToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/capacitador/calificaciones");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CalificacionesPayload>(JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal(capacitacionId, dto!.Capacitacion.Id);
        Assert.Equal("Aprobacion", dto.Capacitacion.TipoCertificacion);
        Assert.Equal(7.0m, dto.Capacitacion.PuntajeMinimo);
        Assert.Equal(2, dto.Asistentes.Count);
        Assert.Equal("Alvarez", dto.Asistentes[0].Apellidos);
        Assert.Equal("Perez", dto.Asistentes[1].Apellidos);
    }

    [Fact]
    public async Task CalificacionesGet_SinToken_Returns401()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/capacitador/calificaciones");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CalificacionesGet_ConTokenCapacitador_Returns403()
    {
        // Un token de role=Capacitador NO debe poder llamar endpoints de role=Calificaciones.
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);

        var token = GenerateCapacitadorToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/capacitador/calificaciones");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CalificacionesGet_EnParticipacion_Returns409()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId, tipo: TipoCertificacion.Participacion);

        // Emitimos el token directamente aun cuando el endpoint admin lo rechazaría:
        // simulamos el caso donde un admin cambió el tipo a Participación después de emitir el token.
        var token = GenerateCalificacionesToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/capacitador/calificaciones");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CalificacionesPut_AsistentePresente_PersisteYResponde200()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId, estadoAsistencia: EstadoAsistencia.Presente);

        var token = GenerateCalificacionesToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync(
            $"/api/capacitador/calificaciones/asistentes/{asistenteId}",
            new { calificacion = 8.75m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CalificacionPayload>(JsonOpts);
        Assert.Equal(8.75m, dto!.Calificacion);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asis = await db.Asistentes.AsNoTracking().FirstAsync(a => a.Id == asistenteId);
        Assert.Equal(8.75m, asis.Calificacion);
    }

    [Fact]
    public async Task CalificacionesPut_AsistenteAusente_Returns409()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId, estadoAsistencia: EstadoAsistencia.Ausente);

        var token = GenerateCalificacionesToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync(
            $"/api/capacitador/calificaciones/asistentes/{asistenteId}",
            new { calificacion = 7m });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorPayload>(JsonOpts);
        Assert.Equal("ASISTENTE_NO_PRESENTE", body!.Error);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public async Task CalificacionesPut_FueraDeRango_Returns400(decimal calif)
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId);

        var token = GenerateCalificacionesToken(factory, capacitacionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync(
            $"/api/capacitador/calificaciones/asistentes/{asistenteId}",
            new { calificacion = calif });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorPayload>(JsonOpts);
        Assert.Equal("CALIFICACION_FUERA_DE_RANGO", body!.Error);
    }

    [Fact]
    public async Task CalificacionesPut_AsistenteDeOtraCapacitacion_Returns404()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionA);
        CreateCapacitacionDirect(factory, out var capacitacionB);
        var asisEnB = CreateAsistenteDirect(factory, capacitacionB, "Otro", "Aitante", "9999");

        // Token con cid=A intenta calificar asistente de B → 404.
        var token = GenerateCalificacionesToken(factory, capacitacionA);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync(
            $"/api/capacitador/calificaciones/asistentes/{asisEnB}",
            new { calificacion = 7m });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminCalificar_PresenteYRango_Returns200YActualiza()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId);

        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/calificacion",
            new { calificacion = 9.2m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CalificacionPayload>(JsonOpts);
        Assert.Equal(9.2m, dto!.Calificacion);
    }

    [Fact]
    public async Task AdminCalificar_EnParticipacion_Returns409()
    {
        using var factory = new InMemoryWebAppFactory();
        EnsureSeeded(factory);
        CreateCapacitacionDirect(factory, out var capacitacionId, tipo: TipoCertificacion.Participacion);
        var asistenteId = CreateAsistenteDirect(factory, capacitacionId, estadoAsistencia: EstadoAsistencia.Presente);

        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/calificacion",
            new { calificacion = 7m });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // --- Payloads locales ---
    private sealed class LinkPayload
    {
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    private sealed class CalificacionesPayload
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
        public string TipoCertificacion { get; set; } = string.Empty;
        public decimal? PuntajeMinimo { get; set; }
    }

    private sealed class AsistentePayload
    {
        public Guid Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string Identificacion { get; set; } = string.Empty;
        public string? EstadoAsistencia { get; set; }
        public decimal? Calificacion { get; set; }
    }

    private sealed class CalificacionPayload
    {
        public Guid Id { get; set; }
        public decimal? Calificacion { get; set; }
    }

    private sealed class ErrorPayload
    {
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
