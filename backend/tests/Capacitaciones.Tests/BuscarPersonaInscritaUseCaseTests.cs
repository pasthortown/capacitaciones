using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>Tests unitarios de <see cref="BuscarPersonaInscritaUseCase"/> (autocompletado por cédula).</summary>
public class BuscarPersonaInscritaUseCaseTests
{
    private static PersonaInscrita BuildPersona(Guid? areaId, string? firma = "data:image/png;base64,AAA==") => new()
    {
        Id = Guid.NewGuid(),
        Identificacion = "1712345678",
        Nombres = "María Fernanda",
        Apellidos = "Pérez Torres",
        AreaId = areaId,
        EmailUsuario = "maria.perez@dos.com.ec",
        Firma = firma,
        FechaCreacion = DateTime.UtcNow,
        FechaActualizacion = DateTime.UtcNow
    };

    [Fact]
    public async Task Encontrada_DevuelveDatosSinSufijoYTieneFirma()
    {
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(area.Id)), new FakeAreas(area));

        var dto = await useCase.ExecuteAsync(" 1712345678 ");

        Assert.NotNull(dto);
        Assert.Equal("María Fernanda", dto!.Nombres);
        Assert.Equal("Pérez Torres", dto.Apellidos);
        Assert.Equal(area.Id, dto.AreaId);
        Assert.Equal("maria.perez", dto.EmailUsuario);
        Assert.True(dto.TieneFirma);
    }

    [Fact]
    public async Task SinFirma_TieneFirmaFalse()
    {
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(null, firma: null)), new FakeAreas(null));

        var dto = await useCase.ExecuteAsync("1712345678");

        Assert.False(dto!.TieneFirma);
        Assert.Null(dto.AreaId);
    }

    [Fact]
    public async Task AreaInactiva_DevuelveAreaIdNull()
    {
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = false, FechaCreacion = DateTime.UtcNow };
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(area.Id)), new FakeAreas(area));

        var dto = await useCase.ExecuteAsync("1712345678");

        Assert.Null(dto!.AreaId);
    }

    [Fact]
    public async Task NoEncontrada_DevuelveNull()
    {
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(), new FakeAreas(null));

        Assert.Null(await useCase.ExecuteAsync("0999999999"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IdentificacionVacia_DevuelveNull(string identificacion)
    {
        var useCase = new BuscarPersonaInscritaUseCase(new FakePersonas(BuildPersona(null)), new FakeAreas(null));

        Assert.Null(await useCase.ExecuteAsync(identificacion));
    }

    private sealed class FakePersonas : IPersonaInscritaRepository
    {
        private readonly Dictionary<string, PersonaInscrita> _store = new();
        public FakePersonas(params PersonaInscrita[] existentes)
        {
            foreach (var p in existentes) _store[p.Identificacion] = p;
        }
        public Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(identificacion, out var p) ? p : null);
        public void Agregar(PersonaInscrita entity) => throw new NotImplementedException();
    }

    private sealed class FakeAreas : IAreaRepository
    {
        private readonly Area? _area;
        public FakeAreas(Area? area) { _area = area; }
        public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_area is not null && _area.Id == id ? _area : null);
        public Task<IEnumerable<Area>> ListAsync(bool includeInactive = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Area?> GetByNombreAsync(string nombre, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Area entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Area> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Area entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
