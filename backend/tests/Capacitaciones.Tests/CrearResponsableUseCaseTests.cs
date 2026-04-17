using Capacitaciones.Application.Dtos.Responsables;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Responsables;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso admin <see cref="CrearResponsableUseCase"/>.
/// Usan un <see cref="FakeResponsableRepository"/> en memoria — sin EF ni HTTP.
/// </summary>
public class CrearResponsableUseCaseTests
{
    [Fact]
    public async Task Crea_ResponsableConFirma_OK()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new CrearResponsableUseCase(repo);

        var dto = await useCase.ExecuteAsync(new CreateResponsableDto
        {
            Nombres = "Ana Pérez",
            Cargo = "Coordinadora",
            Empresa = "DOS",
            Firma = "data:image/png;base64,AAAA"
        });

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Ana Pérez", dto.Nombres);
        Assert.Equal("Coordinadora", dto.Cargo);
        Assert.Equal("DOS", dto.Empresa);
        Assert.True(dto.TieneFirma);
        Assert.Equal("data:image/png;base64,AAAA", dto.Firma);
        Assert.True(dto.Activo);
        Assert.Single(repo.Added);
    }

    [Fact]
    public async Task Crea_FirmaOpcional_AceptaNull()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new CrearResponsableUseCase(repo);

        var dto = await useCase.ExecuteAsync(new CreateResponsableDto
        {
            Nombres = "Bruno",
            Cargo = "Jefe",
            Empresa = "DOS",
            Firma = null
        });

        Assert.False(dto.TieneFirma);
        Assert.Null(dto.Firma);
    }

    [Fact]
    public async Task Crea_FirmaWhitespace_SeNormalizaANull()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new CrearResponsableUseCase(repo);

        var dto = await useCase.ExecuteAsync(new CreateResponsableDto
        {
            Nombres = "Clara",
            Cargo = "Gerente",
            Empresa = "DOS",
            Firma = "   "
        });

        Assert.False(dto.TieneFirma);
        Assert.Null(dto.Firma);
    }

    [Fact]
    public async Task Crea_ValoresConEspacios_SeAplicaTrim()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new CrearResponsableUseCase(repo);

        var dto = await useCase.ExecuteAsync(new CreateResponsableDto
        {
            Nombres = "  Daniela  ",
            Cargo = "  Coordinadora\t",
            Empresa = "  DOS  ",
            Firma = "  data:image/png;base64,YY==  "
        });

        Assert.Equal("Daniela", dto.Nombres);
        Assert.Equal("Coordinadora", dto.Cargo);
        Assert.Equal("DOS", dto.Empresa);
        Assert.Equal("data:image/png;base64,YY==", dto.Firma);
    }

    [Fact]
    public async Task Crea_SinNombres_LanzaServiceException()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new CrearResponsableUseCase(repo);

        var ex = await Assert.ThrowsAsync<ResponsableServiceException>(() => useCase.ExecuteAsync(new CreateResponsableDto
        {
            Nombres = "   ",
            Cargo = "Cargo",
            Empresa = "DOS"
        }));

        Assert.Equal("INVALID_NOMBRES", ex.Codigo);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task Crea_SinCargo_LanzaServiceException()
    {
        var repo = new FakeResponsableRepository();
        var useCase = new CrearResponsableUseCase(repo);

        var ex = await Assert.ThrowsAsync<ResponsableServiceException>(() => useCase.ExecuteAsync(new CreateResponsableDto
        {
            Nombres = "Ana",
            Cargo = "",
            Empresa = "DOS"
        }));

        Assert.Equal("INVALID_CARGO", ex.Codigo);
    }

    // ----- Fake -----

    private sealed class FakeResponsableRepository : IResponsableRepository
    {
        public List<Responsable> Added { get; } = new();

        public Task AddAsync(Responsable entity, CancellationToken ct = default)
        {
            Added.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Responsable>> ListAsync(bool includeInactive, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Responsable?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task UpdateAsync(Responsable entity, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task SetInactivoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> ExistsActivoAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<bool> ExistenActivosAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
