using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso <see cref="CrearCapacitacionUseCase"/> centrados en la
/// validación del nuevo shape <c>responsableIds: Guid[]</c> (refactor Responsables).
/// </summary>
public class CrearCapacitacionUseCaseTests
{
    private static readonly Guid ModalidadId = Guid.NewGuid();
    private static readonly Guid TipoActividadId = Guid.NewGuid();
    private static readonly Guid ResponsableActivoId = Guid.NewGuid();
    private static readonly Guid ResponsableInactivoId = Guid.NewGuid();

    private static CreateCapacitacionDto BuildDto(List<Guid>? responsableIds = null) => new()
    {
        Tema = "Tema test",
        Capacitador = "Ana",
        CargoCapacitador = null,
        EmpresaCapacitador = null,
        ModalidadId = ModalidadId,
        TipoActividadId = TipoActividadId,
        TipoCertificacion = "Participacion",
        FechaHoraInicio = DateTime.UtcNow.AddDays(1),
        DuracionMinutos = 60,
        Descripcion = null,
        ResponsableIds = responsableIds ?? new List<Guid> { ResponsableActivoId }
    };

    private static CrearCapacitacionUseCase BuildUseCase(FakeResponsableRepo? resp = null)
    {
        var modalidad = new Modalidad { Id = ModalidadId, Nombre = "Presencial", Activo = true, FechaCreacion = DateTime.UtcNow };
        var tipo = new TipoActividad { Id = TipoActividadId, Nombre = "Charla", Activo = true, FechaCreacion = DateTime.UtcNow };
        return new CrearCapacitacionUseCase(
            new FakeCapacitacionRepo(),
            new FakeModalidadRepo(modalidad),
            new FakeTipoActividadRepo(tipo),
            resp ?? new FakeResponsableRepo(new[] { ResponsableActivoId }),
            new FakeNumeracionService());
    }

    [Fact]
    public async Task Crea_ResponsableIdDuplicado_LanzaServiceException()
    {
        var useCase = BuildUseCase();
        var dto = BuildDto(new List<Guid> { ResponsableActivoId, ResponsableActivoId });

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() => useCase.ExecuteAsync(dto));
        Assert.Equal("RESPONSABLE_DUPLICADO", ex.Codigo);
    }

    [Fact]
    public async Task Crea_ResponsableIdVacio_LanzaServiceException()
    {
        var useCase = BuildUseCase();
        var dto = BuildDto(new List<Guid> { Guid.Empty });

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() => useCase.ExecuteAsync(dto));
        Assert.Equal("INVALID_RESPONSABLE", ex.Codigo);
    }

    [Fact]
    public async Task Crea_ResponsableInactivoOInexistente_LanzaServiceException()
    {
        // Repo solo considera activo a ResponsableActivoId — el inactivo y un id random fallan.
        var useCase = BuildUseCase(new FakeResponsableRepo(new[] { ResponsableActivoId }));

        // Pasamos un id que NO está en la lista de activos.
        var dto = BuildDto(new List<Guid> { ResponsableInactivoId });

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() => useCase.ExecuteAsync(dto));
        Assert.Equal("INVALID_RESPONSABLE", ex.Codigo);
    }

    // ----- Fakes -----

    private sealed class FakeCapacitacionRepo : ICapacitacionRepository
    {
        public Task<string?> GetLatestFirmaCapacitadorByNombreAsync(string capacitador, Guid? excluirCapacitacionId = null, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<string> AddAsync(Capacitacion entity, Func<CancellationToken, Task<string>> codeFactory, CancellationToken ct = default)
        {
            entity.Codigo = "CAP-PC-REG-999";
            return Task.FromResult(entity.Codigo);
        }

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Capacitacion?>(null);

        public Task<IReadOnlyList<Capacitacion>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
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

    private sealed class FakeModalidadRepo : IModalidadRepository
    {
        private readonly Modalidad _m;
        public FakeModalidadRepo(Modalidad m) { _m = m; }

        public Task<Modalidad?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Modalidad?>(id == _m.Id ? _m : null);

        public Task<IEnumerable<Modalidad>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Modalidad?> GetByNombreAsync(string nombre, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Modalidad entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Modalidad> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Modalidad entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeTipoActividadRepo : ITipoActividadRepository
    {
        private readonly TipoActividad _t;
        public FakeTipoActividadRepo(TipoActividad t) { _t = t; }

        public Task<TipoActividad?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<TipoActividad?>(id == _t.Id ? _t : null);

        public Task<IEnumerable<TipoActividad>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<TipoActividad?> GetByNombreAsync(string nombre, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(TipoActividad entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<TipoActividad> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(TipoActividad entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeResponsableRepo : IResponsableRepository
    {
        private readonly HashSet<Guid> _activos;

        public FakeResponsableRepo(IEnumerable<Guid> activos)
        {
            _activos = new HashSet<Guid>(activos);
        }

        public Task<bool> ExistenActivosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var result = ids.All(id => _activos.Contains(id));
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Responsable>> ListAsync(bool includeInactive, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Responsable?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task AddAsync(Responsable entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateAsync(Responsable entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task SetInactivoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<bool> ExistsActivoAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_activos.Contains(id));
    }

    private sealed class FakeNumeracionService : INumeracionService
    {
        public Task<string> ClaimNextCodeAsync(CancellationToken ct = default)
            => Task.FromResult("CAP-PC-REG-999");
    }
}
