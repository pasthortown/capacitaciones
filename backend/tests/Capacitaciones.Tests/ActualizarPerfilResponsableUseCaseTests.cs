using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Responsable;
using Capacitaciones.Application.UseCases.Responsables;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso público <see cref="ActualizarPerfilResponsableUseCase"/>
/// (perfil del responsable vía link firmado — firma OBLIGATORIA).
/// </summary>
public class ActualizarPerfilResponsableUseCaseTests
{
    private static Responsable NewEntity(bool activo = true) => new()
    {
        Id = Guid.NewGuid(),
        Nombres = "Original Nombre",
        Cargo = "Original Cargo",
        Empresa = "Original Empresa",
        Firma = null,
        Activo = activo,
        FechaCreacion = DateTime.UtcNow.AddDays(-2),
        FechaActualizacion = null
    };

    [Fact]
    public async Task Actualiza_HappyPath_GuardaFirmaYSeteaFechaActualizacion()
    {
        var entity = NewEntity();
        var repo = new FakeResponsableRepository(entity);
        var useCase = new ActualizarPerfilResponsableUseCase(repo);

        var antesUtc = DateTime.UtcNow;
        var dto = await useCase.ExecuteAsync(entity.Id, new UpdateResponsablePerfilDto
        {
            Nombres = "Nuevo Nombre",
            Cargo = "Nuevo Cargo",
            Empresa = "DOS",
            Firma = "data:image/png;base64,ZZ=="
        });

        Assert.Equal("Nuevo Nombre", entity.Nombres);
        Assert.Equal("Nuevo Cargo", entity.Cargo);
        Assert.Equal("DOS", entity.Empresa);
        Assert.Equal("data:image/png;base64,ZZ==", entity.Firma);
        Assert.NotNull(entity.FechaActualizacion);
        Assert.True(entity.FechaActualizacion >= antesUtc);
        Assert.Equal(1, repo.UpdateCallCount);

        Assert.Equal("Nuevo Nombre", dto.Nombres);
        Assert.Equal("data:image/png;base64,ZZ==", dto.Firma);
    }

    [Fact]
    public async Task Actualiza_TrimeaCampos()
    {
        var entity = NewEntity();
        var repo = new FakeResponsableRepository(entity);
        var useCase = new ActualizarPerfilResponsableUseCase(repo);

        await useCase.ExecuteAsync(entity.Id, new UpdateResponsablePerfilDto
        {
            Nombres = "  Con Espacios  ",
            Cargo = "\tCargo\n",
            Empresa = "  DOS  ",
            Firma = "  data:image/png;base64,AA==  "
        });

        Assert.Equal("Con Espacios", entity.Nombres);
        Assert.Equal("Cargo", entity.Cargo);
        Assert.Equal("DOS", entity.Empresa);
        Assert.Equal("data:image/png;base64,AA==", entity.Firma);
    }

    [Fact]
    public async Task Actualiza_FirmaVacia_Lanza400()
    {
        var entity = NewEntity();
        var repo = new FakeResponsableRepository(entity);
        var useCase = new ActualizarPerfilResponsableUseCase(repo);

        var ex = await Assert.ThrowsAsync<ResponsableServiceException>(() => useCase.ExecuteAsync(entity.Id, new UpdateResponsablePerfilDto
        {
            Nombres = "Ana",
            Cargo = "Coord",
            Empresa = "DOS",
            Firma = ""
        }));

        Assert.Equal("INVALID_FIRMA", ex.Codigo);
        Assert.Equal(0, repo.UpdateCallCount);
    }

    [Fact]
    public async Task Actualiza_FirmaWhitespace_Lanza400()
    {
        var entity = NewEntity();
        var repo = new FakeResponsableRepository(entity);
        var useCase = new ActualizarPerfilResponsableUseCase(repo);

        var ex = await Assert.ThrowsAsync<ResponsableServiceException>(() => useCase.ExecuteAsync(entity.Id, new UpdateResponsablePerfilDto
        {
            Nombres = "Ana",
            Cargo = "Coord",
            Empresa = "DOS",
            Firma = "   "
        }));

        Assert.Equal("INVALID_FIRMA", ex.Codigo);
    }

    [Fact]
    public async Task Actualiza_ResponsableInexistente_LanzaNotFound()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new ActualizarPerfilResponsableUseCase(repo);

        await Assert.ThrowsAsync<ResponsableNotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), new UpdateResponsablePerfilDto
        {
            Nombres = "X",
            Cargo = "Y",
            Empresa = "Z",
            Firma = "data:image/png;base64,WW=="
        }));
    }

    [Fact]
    public async Task Actualiza_ResponsableInactivo_LanzaForbidden()
    {
        var entity = NewEntity(activo: false);
        var repo = new FakeResponsableRepository(entity);
        var useCase = new ActualizarPerfilResponsableUseCase(repo);

        await Assert.ThrowsAsync<ResponsableForbiddenException>(() => useCase.ExecuteAsync(entity.Id, new UpdateResponsablePerfilDto
        {
            Nombres = "X",
            Cargo = "Y",
            Empresa = "Z",
            Firma = "data:image/png;base64,WW=="
        }));

        Assert.Equal("Original Nombre", entity.Nombres); // no se tocó
    }

    // ----- Fake -----

    private sealed class FakeResponsableRepository : IResponsableRepository
    {
        private readonly Responsable? _entity;
        public int UpdateCallCount { get; private set; }

        public FakeResponsableRepository(Responsable? entity = null)
        {
            _entity = entity;
        }

        public Task<Responsable?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Responsable?>(null);
            return Task.FromResult<Responsable?>(_entity);
        }

        public Task UpdateAsync(Responsable entity, CancellationToken ct = default)
        {
            UpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Responsable>> ListAsync(bool includeInactive, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task AddAsync(Responsable entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task SetInactivoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<bool> ExistsActivoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<bool> ExistenActivosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
