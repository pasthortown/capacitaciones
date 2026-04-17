using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios de <see cref="ObtenerInscripcionPublicaUseCase"/>.
/// </summary>
public class ObtenerInscripcionPublicaUseCaseTests
{
    private static Capacitacion BuildCapacitacion(bool finalizada)
    {
        var fecha = finalizada ? DateTime.UtcNow.AddHours(-5) : DateTime.UtcNow.AddDays(1);
        return new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = "CAP-PC-REG-007",
            Tema = "Inducción",
            Capacitador = "Ana",
            ModalidadId = Guid.NewGuid(),
            Modalidad = new Modalidad { Id = Guid.NewGuid(), Nombre = "Virtual", Activo = true, FechaCreacion = DateTime.UtcNow },
            TipoActividadId = Guid.NewGuid(),
            TipoActividad = new TipoActividad { Id = Guid.NewGuid(), Nombre = "Seminario", Activo = true, FechaCreacion = DateTime.UtcNow },
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = fecha,
            DuracionMinutos = 60,
            Activo = true,
            FechaCreacion = DateTime.UtcNow.AddDays(-3)
        };
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_DevuelveCapacitacionYAreas()
    {
        var cap = BuildCapacitacion(finalizada: false);
        var areas = new List<Area>
        {
            new() { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Nombre = "Finanzas", Activo = true, FechaCreacion = DateTime.UtcNow }
        };

        var useCase = new ObtenerInscripcionPublicaUseCase(
            new StubCapRepo(cap),
            new StubAreaRepo(areas));

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal("CAP-PC-REG-007", dto.Capacitacion.Codigo);
        Assert.Equal("Inducción", dto.Capacitacion.Tema);
        Assert.Equal("Virtual", dto.Capacitacion.Modalidad.Nombre);
        Assert.Equal("Seminario", dto.Capacitacion.TipoActividad.Nombre);
        Assert.Equal("Inscripciones Abiertas", dto.Capacitacion.Estado);
        Assert.Equal(2, dto.Areas.Count);
        // Áreas ordenadas por nombre.
        Assert.Equal("Finanzas", dto.Areas[0].Nombre);
        Assert.Equal("TI", dto.Areas[1].Nombre);
    }

    [Fact]
    public async Task ExecuteAsync_Finalizada_LanzaInscripcionCerrada()
    {
        var cap = BuildCapacitacion(finalizada: true);
        var useCase = new ObtenerInscripcionPublicaUseCase(
            new StubCapRepo(cap),
            new StubAreaRepo(new List<Area>()));

        await Assert.ThrowsAsync<InscripcionCerradaException>(() => useCase.ExecuteAsync(cap.Id));
    }

    [Fact]
    public async Task ExecuteAsync_Inexistente_LanzaNotFound()
    {
        var useCase = new ObtenerInscripcionPublicaUseCase(
            new StubCapRepo(null),
            new StubAreaRepo(new List<Area>()));

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid()));
    }

    private sealed class StubCapRepo : ICapacitacionRepository
    {
        private readonly Capacitacion? _entity;
        public StubCapRepo(Capacitacion? entity) { _entity = entity; }

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_entity is not null && _entity.Id == id ? _entity : null);

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

    private sealed class StubAreaRepo : IAreaRepository
    {
        private readonly List<Area> _areas;
        public StubAreaRepo(List<Area> areas) { _areas = areas; }

        public Task<IEnumerable<Area>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            IEnumerable<Area> q = _areas;
            if (!includeInactive) q = q.Where(a => a.Activo);
            return Task.FromResult(q);
        }

        public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_areas.FirstOrDefault(a => a.Id == id));

        public Task<Area?> GetByNombreAsync(string nombre, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task AddAsync(Area entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Area> entities, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task UpdateAsync(Area entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
