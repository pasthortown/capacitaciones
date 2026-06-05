using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.PaseLista;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso Fase 10 <see cref="ObtenerPaseListaUseCase"/>.
/// El test clave es el orden alfabético por <c>Apellidos</c> y luego <c>Nombres</c>
/// (case-insensitive), que la UI del capacitador recorre uno por uno.
/// </summary>
public class ObtenerPaseListaUseCaseTests
{
    private static Capacitacion BuildCapacitacion(bool activo = true) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "CAP-PC-REG-001",
        Tema = "Tema",
        Capacitador = "Ana",
        ModalidadId = Guid.NewGuid(),
        TipoActividadId = Guid.NewGuid(),
        TipoCertificacion = TipoCertificacion.Participacion,
        FechaHoraInicio = DateTime.UtcNow.AddDays(1),
        DuracionMinutos = 60,
        Activo = activo,
        FechaCreacion = DateTime.UtcNow.AddDays(-2)
    };

    private static Asistente BuildAsistente(Guid capacitacionId, string nombres, string apellidos) => new()
    {
        Id = Guid.NewGuid(),
        CapacitacionId = capacitacionId,
        Nombres = nombres,
        Apellidos = apellidos,
        Identificacion = Guid.NewGuid().ToString("N").Substring(0, 10),
        AreaId = Guid.NewGuid(),
        EmailUsuario = $"{nombres}.{apellidos}@dos.com.ec",
        Firma = "data:image/png;base64,AAA==",
        FechaInscripcion = DateTime.UtcNow
    };

    [Fact]
    public async Task ExecuteAsync_OrdenaAlfabeticamentePorApellidosYNombres()
    {
        var cap = BuildCapacitacion();
        // Insertamos en desorden. Esperamos orden: "Alvarez Ana", "Perez Juan", "Perez Maria", "Zuñiga Luis".
        // Notar dos "Perez" con nombres distintos: debe romper empate por Nombres.
        var asistentes = new List<Asistente>
        {
            BuildAsistente(cap.Id, "Luis", "Zuñiga"),
            BuildAsistente(cap.Id, "Maria", "Perez"),
            BuildAsistente(cap.Id, "Ana", "Alvarez"),
            BuildAsistente(cap.Id, "Juan", "Perez")
        };

        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(asistentes);
        var useCase = new ObtenerPaseListaUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal(4, dto.Asistentes.Count);
        Assert.Equal("Alvarez", dto.Asistentes[0].Apellidos);
        Assert.Equal("Perez", dto.Asistentes[1].Apellidos);
        Assert.Equal("Juan", dto.Asistentes[1].Nombres);
        Assert.Equal("Perez", dto.Asistentes[2].Apellidos);
        Assert.Equal("Maria", dto.Asistentes[2].Nombres);
        Assert.Equal("Zuñiga", dto.Asistentes[3].Apellidos);
    }

    [Fact]
    public async Task ExecuteAsync_OrdenEsCaseInsensitive()
    {
        var cap = BuildCapacitacion();
        var asistentes = new List<Asistente>
        {
            BuildAsistente(cap.Id, "b", "bravo"),
            BuildAsistente(cap.Id, "a", "ALPHA"),
            BuildAsistente(cap.Id, "c", "Charlie")
        };

        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(asistentes);
        var useCase = new ObtenerPaseListaUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal("ALPHA", dto.Asistentes[0].Apellidos);
        Assert.Equal("bravo", dto.Asistentes[1].Apellidos);
        Assert.Equal("Charlie", dto.Asistentes[2].Apellidos);
    }

    [Fact]
    public async Task ExecuteAsync_DevuelveEstadoYFechaComoEstan()
    {
        var cap = BuildCapacitacion();
        var a1 = BuildAsistente(cap.Id, "Ana", "Alvarez");
        a1.EstadoAsistencia = EstadoAsistencia.Presente;
        a1.FechaMarcacionAsistencia = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var a2 = BuildAsistente(cap.Id, "Bob", "Bueno");
        // a2 queda null/null (sin registrar).

        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(new[] { a1, a2 });
        var useCase = new ObtenerPaseListaUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal("Presente", dto.Asistentes[0].EstadoAsistencia);
        Assert.Equal(a1.FechaMarcacionAsistencia, dto.Asistentes[0].FechaMarcacionAsistencia);
        Assert.Null(dto.Asistentes[1].EstadoAsistencia);
        Assert.Null(dto.Asistentes[1].FechaMarcacionAsistencia);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInexistente_LanzaNotFound()
    {
        var capRepo = new FakeCapacitacionRepository();
        var asisRepo = new FakeAsistenteRepo(Array.Empty<Asistente>());
        var useCase = new ObtenerPaseListaUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInactiva_LanzaForbidden()
    {
        var cap = BuildCapacitacion(activo: false);
        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(Array.Empty<Asistente>());
        var useCase = new ObtenerPaseListaUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitadorForbiddenException>(() => useCase.ExecuteAsync(cap.Id));
    }

    private sealed class FakeCapacitacionRepository : ICapacitacionRepository
    {
        private readonly Capacitacion? _entity;
        public FakeCapacitacionRepository(Capacitacion? entity = null) { _entity = entity; }

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Capacitacion?>(null);
            return Task.FromResult<Capacitacion?>(_entity);
        }

        public Task<IReadOnlyList<Capacitacion>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<string> AddAsync(Capacitacion entity, Func<CancellationToken, Task<string>> codeFactory, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateWithResponsablesAsync(Capacitacion entity, IEnumerable<CapacitacionResponsable> nuevasRelaciones, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateAsync(Capacitacion entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteLogicoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeAsistenteRepo : IAsistenteRepository
    {
        private readonly IList<Asistente> _items;
        public FakeAsistenteRepo(IEnumerable<Asistente> items) { _items = items.ToList(); }

        public Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Asistente>>(_items.Where(a => a.CapacitacionId == capacitacionId).ToList());

        public Task AddAsync(Asistente entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Asistente?> GetByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<bool> ExistsByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateAsync(Asistente entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<int> CountByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, int>> CountByCapacitacionesAsync(IEnumerable<Guid> capacitacionIds, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
