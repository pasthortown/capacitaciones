using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Capacitaciones.Tests;

/// <summary>
/// Integración de <c>POST api/capacitaciones/{id}/asistentes/{asistenteId}/certificado/enviar</c>.
/// La cola se reemplaza por una que solo registra lo encolado (el worker nunca recibe nada), así
/// el estado <c>Pendiente</c> se puede afirmar sin que el envío real lo cambie.
/// </summary>
public class EnviarCertificadoIndividualEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly RecordingQueue _queue = new();

    private static readonly Guid ModalidadId = new("11111111-1111-1111-1111-111111110040");
    private static readonly Guid TipoActividadId = new("22222222-2222-2222-2222-222222220040");
    private static readonly Guid AreaId = new("44444444-4444-4444-4444-444444440040");

    public EnviarCertificadoIndividualEndpointTests(InMemoryWebAppFactory baseFactory)
    {
        _factory = baseFactory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICertificadoEnvioQueue>();
            services.AddSingleton<ICertificadoEnvioQueue>(_queue);
        }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (!db.Modalidades.Any(m => m.Id == ModalidadId))
            db.Modalidades.Add(new Modalidad { Id = ModalidadId, Nombre = "Presencial", Activo = true, FechaCreacion = t0 });
        if (!db.TiposActividad.Any(t => t.Id == TipoActividadId))
            db.TiposActividad.Add(new TipoActividad { Id = TipoActividadId, Nombre = "Charla", Activo = true, FechaCreacion = t0 });
        if (!db.Areas.Any(a => a.Id == AreaId))
            db.Areas.Add(new Area { Id = AreaId, Nombre = "Area envio", Activo = true, FechaCreacion = t0 });
        db.SaveChanges();
    }

    private Guid CrearCapacitacion(bool finalizada = true, bool emiteCertificado = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cap = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = $"CAP-PC-ENV-{Random.Shared.Next(100, 999):000}",
            Tema = "Tema envío",
            Capacitador = "Ana",
            ModalidadId = ModalidadId,
            TipoActividadId = TipoActividadId,
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = finalizada ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(1),
            DuracionMinutos = 60,
            EmiteCertificado = emiteCertificado,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        db.Capacitaciones.Add(cap);
        db.SaveChanges();
        return cap.Id;
    }

    private Guid CrearAsistente(
        Guid capacitacionId,
        EstadoAsistencia? asistencia,
        EstadoEnvioCertificado? envio = null,
        string? mensajeError = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var a = new Asistente
        {
            Id = Guid.NewGuid(),
            CapacitacionId = capacitacionId,
            Nombres = "Juan",
            Apellidos = "Perez",
            Identificacion = Random.Shared.Next(100000000, 999999999).ToString(),
            AreaId = AreaId,
            EmailUsuario = "juan.perez@dos.com.ec",
            Firma = "data:image/png;base64,AAA=",
            FechaInscripcion = DateTime.UtcNow,
            EstadoAsistencia = asistencia,
            EstadoEnvioCertificado = envio,
            MensajeErrorEnvio = mensajeError
        };
        db.Asistentes.Add(a);
        db.SaveChanges();
        return a.Id;
    }

    private Asistente Leer(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Asistentes.AsNoTracking().Single(a => a.Id == id);
    }

    private HttpClient AdminClient()
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
        var admin = scope.ServiceProvider.GetRequiredService<AppDbContext>().AdminUsers.AsNoTracking().First();
        var token = jwt.Generate(admin).Token;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpResponseMessage> Enviar(Guid capacitacionId, Guid asistenteId) =>
        AdminClient().PostAsync($"/api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/certificado/enviar", null);

    private static async Task<string?> CodigoError(HttpResponseMessage resp)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("error").GetString();
    }

    [Fact]
    public async Task Presente_MarcaSoloEsePendienteYEncola()
    {
        var cap = CrearCapacitacion();
        var id = CrearAsistente(cap, EstadoAsistencia.Presente, EstadoEnvioCertificado.Error, "fallo previo");
        var otro = CrearAsistente(cap, EstadoAsistencia.Presente, EstadoEnvioCertificado.Enviado);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var a = Leer(id);
        Assert.Equal(EstadoEnvioCertificado.Pendiente, a.EstadoEnvioCertificado);
        Assert.Null(a.MensajeErrorEnvio);
        Assert.Equal(EstadoEnvioCertificado.Enviado, Leer(otro).EstadoEnvioCertificado);
        Assert.Contains(cap, _queue.Encolados);
    }

    [Fact]
    public async Task YaEnviado_PermiteReenviar()
    {
        var cap = CrearCapacitacion();
        var id = CrearAsistente(cap, EstadoAsistencia.Presente, EstadoEnvioCertificado.Enviado);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        Assert.Equal(EstadoEnvioCertificado.Pendiente, Leer(id).EstadoEnvioCertificado);
    }

    [Fact]
    public async Task Ausente_Devuelve409YNoEncola()
    {
        var cap = CrearCapacitacion();
        var id = CrearAsistente(cap, EstadoAsistencia.Ausente);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("ASISTENTE_NO_ELEGIBLE_CERTIFICADO", await CodigoError(resp));
        Assert.Null(Leer(id).EstadoEnvioCertificado);
        Assert.DoesNotContain(cap, _queue.Encolados);
    }

    [Fact]
    public async Task SinMarcar_Devuelve409()
    {
        var cap = CrearCapacitacion();
        var id = CrearAsistente(cap, null);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("ASISTENTE_NO_ELEGIBLE_CERTIFICADO", await CodigoError(resp));
    }

    [Fact]
    public async Task CapacitacionNoFinalizada_Devuelve409()
    {
        var cap = CrearCapacitacion(finalizada: false);
        var id = CrearAsistente(cap, EstadoAsistencia.Presente);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Null(Leer(id).EstadoEnvioCertificado);
    }

    [Fact]
    public async Task NoEmiteCertificado_Devuelve409()
    {
        var cap = CrearCapacitacion(emiteCertificado: false);
        var id = CrearAsistente(cap, EstadoAsistencia.Presente);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task AsistenteDeOtraCapacitacion_Devuelve404()
    {
        var cap = CrearCapacitacion();
        var otraCap = CrearCapacitacion();
        var id = CrearAsistente(otraCap, EstadoAsistencia.Presente);

        var resp = await Enviar(cap, id);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Null(Leer(id).EstadoEnvioCertificado);
    }

    [Fact]
    public async Task SinToken_Devuelve401()
    {
        var resp = await _factory.CreateClient()
            .PostAsync($"/api/capacitaciones/{Guid.NewGuid()}/asistentes/{Guid.NewGuid()}/certificado/enviar", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>Cola que solo registra; el worker queda esperando para siempre.</summary>
    private sealed class RecordingQueue : ICertificadoEnvioQueue
    {
        public ConcurrentBag<Guid> Encolados { get; } = new();

        public void Encolar(Guid capacitacionId) => Encolados.Add(capacitacionId);

        public async ValueTask<Guid> DequeueAsync(CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Guid.Empty;
        }
    }
}
