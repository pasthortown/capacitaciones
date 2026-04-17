using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests de integración del módulo de Capacitaciones (Fase 3).
/// Cubren: creación secuencial con código atómico, validaciones, update (replace-all de la pivote
/// N–N a responsables), delete lógico y la validación de siguienteNumero vs max emitido.
/// Los responsables se preseedean en el catálogo global antes de referenciarlos en los payloads.
/// </summary>
public class CapacitacionesEndpointTests : IClassFixture<InMemoryWebAppFactory>
{
    private readonly InMemoryWebAppFactory _factory;

    private static readonly Guid SeededModalidadId = new("11111111-1111-1111-1111-111111111001");
    private static readonly Guid SeededTipoActividadId = new("22222222-2222-2222-2222-222222222001");

    // Responsables globales preseedeados (catálogo N–N).
    private static readonly Guid SeededResponsable1Id = new("33333333-3333-3333-3333-333333333001");
    private static readonly Guid SeededResponsable2Id = new("33333333-3333-3333-3333-333333333002");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CapacitacionesEndpointTests(InMemoryWebAppFactory factory)
    {
        _factory = factory;
        EnsureCatalogosSeeded();
    }

    /// <summary>
    /// InMemory no ejecuta migraciones ni aplica HasData por completo; para que los FKs válidos
    /// en los tests funcionen, aseguramos manualmente que existan la modalidad, el tipo y los
    /// responsables del catálogo global usados.
    /// </summary>
    private void EnsureCatalogosSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SeedIfMissing(db);
        db.SaveChanges();
    }

    private static void SeedIfMissing(AppDbContext db)
    {
        if (!db.Modalidades.Any(m => m.Id == SeededModalidadId))
        {
            db.Modalidades.Add(new Capacitaciones.Domain.Entities.Modalidad
            {
                Id = SeededModalidadId,
                Nombre = "Presencial",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.TiposActividad.Any(t => t.Id == SeededTipoActividadId))
        {
            db.TiposActividad.Add(new Capacitaciones.Domain.Entities.TipoActividad
            {
                Id = SeededTipoActividadId,
                Nombre = "Charla",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.Responsables.Any(r => r.Id == SeededResponsable1Id))
        {
            db.Responsables.Add(new Capacitaciones.Domain.Entities.Responsable
            {
                Id = SeededResponsable1Id,
                Nombres = "Responsable Uno",
                Cargo = "Coordinador",
                Empresa = "DOS",
                Firma = "data:image/png;base64,AAAA",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        if (!db.Responsables.Any(r => r.Id == SeededResponsable2Id))
        {
            db.Responsables.Add(new Capacitaciones.Domain.Entities.Responsable
            {
                Id = SeededResponsable2Id,
                Nombres = "Responsable Dos",
                Cargo = "Líder",
                Empresa = "DOS",
                Firma = "data:image/png;base64,BBBB",
                Activo = true,
                FechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
    }

    private static object BuildCreatePayload(
        string tema = "Tema de prueba",
        int duracionMinutos = 60,
        DateTime? fechaHoraInicio = null,
        Guid[]? responsableIds = null)
    {
        return new
        {
            tema,
            capacitador = "Ana Capacitadora",
            cargoCapacitador = "Ingeniera",
            empresaCapacitador = "DOS",
            modalidadId = SeededModalidadId,
            tipoActividadId = SeededTipoActividadId,
            tipoCertificacion = "Participacion",
            fechaHoraInicio = fechaHoraInicio ?? DateTime.UtcNow.AddDays(7),
            duracionMinutos,
            descripcion = (string?)null,
            responsableIds = responsableIds ?? new[] { SeededResponsable1Id }
        };
    }

    [Fact]
    public async Task Crear_PrimeraCapacitacion_AsignaCodigo001()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CapacitacionPayload>(JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal("CAP-PC-REG-001", dto!.Codigo);
        Assert.Equal("Inscripciones Abiertas", dto.Estado);
        Assert.Single(dto.Responsables);
        Assert.Equal(SeededResponsable1Id, dto.Responsables[0].Id);
        Assert.Equal(0, dto.Responsables[0].Orden);
    }

    [Fact]
    public async Task Crear_SegundaCapacitacion_AsignaCodigo002()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var r1 = await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload("Primera"));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload("Segunda"));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var dto = await r2.Content.ReadFromJsonAsync<CapacitacionPayload>(JsonOpts);
        Assert.Equal("CAP-PC-REG-002", dto!.Codigo);
    }

    [Fact]
    public async Task List_IncluyeCapacitacionReciente_ConEstadoInscripcionesAbiertas()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var creada = await (await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload("Para listar")))
            .Content.ReadFromJsonAsync<CapacitacionPayload>(JsonOpts);

        var listResponse = await client.GetAsync("/api/capacitaciones");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var items = await listResponse.Content.ReadFromJsonAsync<List<CapacitacionPayload>>(JsonOpts);
        Assert.NotNull(items);
        var encontrada = items!.FirstOrDefault(c => c.Id == creada!.Id);
        Assert.NotNull(encontrada);
        Assert.Equal("Inscripciones Abiertas", encontrada!.Estado);
        Assert.Equal(0, encontrada.TotalAsistentes);
    }

    [Fact]
    public async Task Crear_DuracionInvalida_Returns400()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload(duracionMinutos: 45));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReemplazaResponsables()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var creada = await (await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload()))
            .Content.ReadFromJsonAsync<CapacitacionPayload>(JsonOpts);

        var updatePayload = new
        {
            tema = creada!.Tema,
            capacitador = creada.Capacitador,
            cargoCapacitador = creada.CargoCapacitador,
            empresaCapacitador = creada.EmpresaCapacitador,
            firmaCapacitador = (string?)null,
            modalidadId = SeededModalidadId,
            tipoActividadId = SeededTipoActividadId,
            tipoCertificacion = "Aprobacion",
            fechaHoraInicio = creada.FechaHoraInicio,
            duracionMinutos = creada.DuracionMinutos,
            descripcion = "Nueva descripcion",
            // Invertimos el orden: primero el 2 (orden 0), luego el 1 (orden 1).
            responsableIds = new[] { SeededResponsable2Id, SeededResponsable1Id }
        };

        var response = await client.PutAsJsonAsync($"/api/capacitaciones/{creada.Id}", updatePayload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<CapacitacionPayload>(JsonOpts);
        Assert.Equal(2, dto!.Responsables.Count);
        Assert.Equal(SeededResponsable2Id, dto.Responsables[0].Id);
        Assert.Equal(0, dto.Responsables[0].Orden);
        Assert.Equal(SeededResponsable1Id, dto.Responsables[1].Id);
        Assert.Equal(1, dto.Responsables[1].Orden);
        Assert.Equal("Aprobacion", dto.TipoCertificacion);

        // Confirmamos en BD: solo deben existir 2 entradas pivote para la capacitación;
        // el catálogo global NO fue modificado (sigue con los 2 responsables preseedeados).
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.CapacitacionResponsables.CountAsync(cr => cr.CapacitacionId == creada.Id);
        Assert.Equal(2, count);
        Assert.True(await db.Responsables.AnyAsync(r => r.Id == SeededResponsable1Id));
        Assert.True(await db.Responsables.AnyAsync(r => r.Id == SeededResponsable2Id));
    }

    [Fact]
    public async Task Crear_ConIdInexistente_Returns400()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/capacitaciones",
            BuildCreatePayload(responsableIds: new[] { Guid.NewGuid() }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Crear_ConIdsDuplicados_Returns409()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/capacitaciones",
            BuildCreatePayload(responsableIds: new[] { SeededResponsable1Id, SeededResponsable1Id }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("RESPONSABLE_DUPLICADO", body);
    }

    [Fact]
    public async Task Delete_MarcaInactiva_ListSinInactivosNoLaTrae()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        var creada = await (await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload("Para borrar")))
            .Content.ReadFromJsonAsync<CapacitacionPayload>(JsonOpts);

        var del = await client.DeleteAsync($"/api/capacitaciones/{creada!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var listActivas = await client.GetFromJsonAsync<List<CapacitacionPayload>>("/api/capacitaciones", JsonOpts);
        Assert.DoesNotContain(listActivas!, c => c.Id == creada.Id);

        var listTodas = await client.GetFromJsonAsync<List<CapacitacionPayload>>("/api/capacitaciones?includeInactive=true", JsonOpts);
        var inactiva = listTodas!.FirstOrDefault(c => c.Id == creada.Id);
        Assert.NotNull(inactiva);
        Assert.False(inactiva!.Activo);
    }

    [Fact]
    public async Task ActualizarNumeracion_SiNuevoValorEsMenorOIgualAMaxEmitido_Returns400()
    {
        using var factory = new InMemoryWebAppFactory();
        await SeedCatalogos(factory);
        var client = await factory.CreateAuthenticatedClientAsync();

        // Emitimos CAP-PC-REG-001 y CAP-PC-REG-002.
        await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload("A"));
        await client.PostAsJsonAsync("/api/capacitaciones", BuildCreatePayload("B"));

        // Intentar fijar el siguienteNumero en 2 debería fallar (max emitido = 2).
        var response = await client.PutAsJsonAsync("/api/configuracion/numeracion", new { siguienteNumero = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CAP-PC-REG-002", body);

        // Fijar en 3 sí debe funcionar.
        var ok = await client.PutAsJsonAsync("/api/configuracion/numeracion", new { siguienteNumero = 3 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    private static Task SeedCatalogos(InMemoryWebAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        SeedIfMissing(db);
        db.SaveChanges();
        return Task.CompletedTask;
    }

    // DTOs de lectura (locales a los tests).
    private sealed class CapacitacionPayload
    {
        public Guid Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Tema { get; set; } = string.Empty;
        public string Capacitador { get; set; } = string.Empty;
        public string? CargoCapacitador { get; set; }
        public string? EmpresaCapacitador { get; set; }
        public string TipoCertificacion { get; set; } = string.Empty;
        public DateTime FechaHoraInicio { get; set; }
        public int DuracionMinutos { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int TotalAsistentes { get; set; }
        public bool Activo { get; set; }
        public List<ResponsablePayload> Responsables { get; set; } = new();
    }

    private sealed class ResponsablePayload
    {
        public Guid Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string? Firma { get; set; }
        public int Orden { get; set; }
    }
}
