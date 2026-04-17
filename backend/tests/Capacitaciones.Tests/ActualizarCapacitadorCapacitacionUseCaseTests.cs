using Capacitaciones.Application.Dtos.Capacitador;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso Fase 4 <see cref="ActualizarCapacitadorCapacitacionUseCase"/>.
/// Usan un <see cref="FakeCapacitacionRepository"/> en memoria — no dependen de EF ni del
/// WebApplicationFactory (no necesitamos validar el pipeline HTTP aquí; eso se ejercita vía
/// los endpoint tests que ya cubren el controlador con JWT real).
/// </summary>
public class ActualizarCapacitadorCapacitacionUseCaseTests
{
    private static Capacitacion NewEntity(bool activo = true) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "CAP-PC-REG-001",
        Tema = "Tema",
        Capacitador = "Ana",
        ModalidadId = Guid.NewGuid(),
        Modalidad = new Modalidad { Id = Guid.NewGuid(), Nombre = "Presencial", Activo = true, FechaCreacion = DateTime.UtcNow },
        TipoActividadId = Guid.NewGuid(),
        TipoActividad = new TipoActividad { Id = Guid.NewGuid(), Nombre = "Charla", Activo = true, FechaCreacion = DateTime.UtcNow },
        TipoCertificacion = TipoCertificacion.Participacion,
        FechaHoraInicio = DateTime.UtcNow.AddDays(-1),
        DuracionMinutos = 60,
        Activo = activo,
        FechaCreacion = DateTime.UtcNow.AddDays(-2),
        FechaActualizacion = null,
        Descripcion = "original",
        CargoCapacitador = "original-cargo",
        EmpresaCapacitador = "original-empresa",
        FirmaCapacitador = "original-firma"
    };

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReemplazaCamposYSeteaFechaActualizacion()
    {
        var entity = NewEntity();
        var repo = new FakeCapacitacionRepository(entity);
        var useCase = new ActualizarCapacitadorCapacitacionUseCase(repo);

        var antesUtc = DateTime.UtcNow;
        var dto = await useCase.ExecuteAsync(entity.Id, new UpdateCapacitadorCapacitacionDto
        {
            Descripcion = "nueva descripcion",
            CargoCapacitador = "Nuevo Cargo",
            EmpresaCapacitador = "Nueva Empresa",
            FirmaCapacitador = "data:image/png;base64,ZZ=="
        });

        Assert.Equal("nueva descripcion", entity.Descripcion);
        Assert.Equal("Nuevo Cargo", entity.CargoCapacitador);
        Assert.Equal("Nueva Empresa", entity.EmpresaCapacitador);
        Assert.Equal("data:image/png;base64,ZZ==", entity.FirmaCapacitador);
        Assert.NotNull(entity.FechaActualizacion);
        Assert.True(entity.FechaActualizacion >= antesUtc);
        Assert.Equal(1, repo.UpdateCallCount);

        // DTO retornado refleja los nuevos valores.
        Assert.Equal("nueva descripcion", dto.Descripcion);
        Assert.Equal("Nuevo Cargo", dto.CargoCapacitador);
        Assert.Equal("Nueva Empresa", dto.EmpresaCapacitador);
        Assert.Equal("data:image/png;base64,ZZ==", dto.FirmaCapacitador);
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceYEmptyString_SeNormalizanANull()
    {
        var entity = NewEntity();
        var repo = new FakeCapacitacionRepository(entity);
        var useCase = new ActualizarCapacitadorCapacitacionUseCase(repo);

        await useCase.ExecuteAsync(entity.Id, new UpdateCapacitadorCapacitacionDto
        {
            Descripcion = "   ",       // solo whitespace
            CargoCapacitador = "",      // string vacío
            EmpresaCapacitador = "\t\n", // whitespace mixto
            FirmaCapacitador = "   "    // la firma también se trimea → null
        });

        Assert.Null(entity.Descripcion);
        Assert.Null(entity.CargoCapacitador);
        Assert.Null(entity.EmpresaCapacitador);
        Assert.Null(entity.FirmaCapacitador);
    }

    [Fact]
    public async Task ExecuteAsync_ValoresConEspaciosAlrededor_SeAplicaTrim()
    {
        var entity = NewEntity();
        var repo = new FakeCapacitacionRepository(entity);
        var useCase = new ActualizarCapacitadorCapacitacionUseCase(repo);

        await useCase.ExecuteAsync(entity.Id, new UpdateCapacitadorCapacitacionDto
        {
            Descripcion = "  desc con espacios  ",
            CargoCapacitador = "  Coordinador ",
            EmpresaCapacitador = " DOS ",
            FirmaCapacitador = "  data:image/png;base64,AAA==  "
        });

        Assert.Equal("desc con espacios", entity.Descripcion);
        Assert.Equal("Coordinador", entity.CargoCapacitador);
        Assert.Equal("DOS", entity.EmpresaCapacitador);
        Assert.Equal("data:image/png;base64,AAA==", entity.FirmaCapacitador);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInexistente_LanzaNotFound()
    {
        var repo = new FakeCapacitacionRepository();
        var useCase = new ActualizarCapacitadorCapacitacionUseCase(repo);

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), new UpdateCapacitadorCapacitacionDto()));
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInactiva_LanzaCapacitadorForbidden()
    {
        var entity = NewEntity(activo: false);
        var repo = new FakeCapacitacionRepository(entity);
        var useCase = new ActualizarCapacitadorCapacitacionUseCase(repo);

        var ex = await Assert.ThrowsAsync<CapacitadorForbiddenException>(() =>
            useCase.ExecuteAsync(entity.Id, new UpdateCapacitadorCapacitacionDto
            {
                Descripcion = "no debe aplicarse"
            }));

        Assert.Contains("inactiva", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.UpdateCallCount);
        Assert.Equal("original", entity.Descripcion); // no se tocó
    }

    /// <summary>
    /// Stub manual de <see cref="ICapacitacionRepository"/>. Solo implementa los métodos usados
    /// por el caso de uso; los demás lanzan <see cref="NotImplementedException"/> porque no
    /// deberían alcanzarse en estos tests.
    /// </summary>
    private sealed class FakeCapacitacionRepository : ICapacitacionRepository
    {
        private readonly Capacitacion? _entity;
        public int UpdateCallCount { get; private set; }

        public FakeCapacitacionRepository(Capacitacion? entity = null)
        {
            _entity = entity;
        }

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Capacitacion?>(null);
            return Task.FromResult<Capacitacion?>(_entity);
        }

        public Task UpdateAsync(Capacitacion entity, CancellationToken ct = default)
        {
            UpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Capacitacion>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<string> AddAsync(Capacitacion entity, Func<CancellationToken, Task<string>> codeFactory, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateWithResponsablesAsync(Capacitacion entity, IEnumerable<CapacitacionResponsable> nuevasRelaciones, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task DeleteLogicoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
