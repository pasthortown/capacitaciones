using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso Fase 5 <see cref="InscribirAsistenteUseCase"/>.
/// Usan fakes manuales — consistentes con el estilo de <see cref="ActualizarCapacitadorCapacitacionUseCaseTests"/>.
/// </summary>
public class InscribirAsistenteUseCaseTests
{
    private static Capacitacion BuildCapacitacion(bool activo = true, bool finalizada = false)
    {
        // finalizada = true → fecha+duración ya en el pasado.
        var fecha = finalizada
            ? DateTime.UtcNow.AddHours(-5)
            : DateTime.UtcNow.AddDays(1);

        return new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = "CAP-PC-REG-001",
            Tema = "Seguridad Industrial",
            Capacitador = "Ana",
            ModalidadId = Guid.NewGuid(),
            Modalidad = new Modalidad { Id = Guid.NewGuid(), Nombre = "Presencial", Activo = true, FechaCreacion = DateTime.UtcNow },
            TipoActividadId = Guid.NewGuid(),
            TipoActividad = new TipoActividad { Id = Guid.NewGuid(), Nombre = "Charla", Activo = true, FechaCreacion = DateTime.UtcNow },
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = fecha,
            DuracionMinutos = 60,
            Activo = activo,
            FechaCreacion = DateTime.UtcNow.AddDays(-2)
        };
    }

    private static CreateInscripcionDto BuildInput(Guid areaId, string identificacion = "1712345678", string emailUsuario = "juan.perez") => new()
    {
        Nombres = "Juan",
        Apellidos = "Perez",
        Identificacion = identificacion,
        AreaId = areaId,
        EmailUsuario = emailUsuario,
        Firma = "data:image/png;base64,AAA=="
    };

    [Fact]
    public async Task ExecuteAsync_HappyPath_CreaAsistenteYConcatenaDominio()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };

        var capRepo = new FakeCapacitacionRepo(capacitacion);
        var areaRepo = new FakeAreaRepo(area);
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new InscribirAsistenteUseCase(capRepo, areaRepo, asisRepo);

        var antes = DateTime.UtcNow;
        var dto = await useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id));

        Assert.Single(asisRepo.Added);
        var added = asisRepo.Added[0];
        Assert.Equal(capacitacion.Id, added.CapacitacionId);
        Assert.Equal("Juan", added.Nombres);
        Assert.Equal("Perez", added.Apellidos);
        Assert.Equal("1712345678", added.Identificacion);
        Assert.Equal(area.Id, added.AreaId);
        Assert.Equal("juan.perez@dos.com.ec", added.EmailUsuario);
        Assert.Equal("data:image/png;base64,AAA==", added.Firma);
        Assert.True(added.FechaInscripcion >= antes);

        // DTO retornado
        Assert.Equal("juan.perez@dos.com.ec", dto.Email);
        Assert.Equal(area.Id, dto.Area.Id);
        Assert.Equal("TI", dto.Area.Nombre);
    }

    [Fact]
    public async Task ExecuteAsync_TrimeaCamposAntesDeGuardar()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };

        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion),
            new FakeAreaRepo(area),
            new FakeAsistenteRepo(out var asisRepo));

        await useCase.ExecuteAsync(capacitacion.Id, new CreateInscripcionDto
        {
            Nombres = "  Juan  ",
            Apellidos = "\tPerez\n",
            Identificacion = " 1712345678 ",
            AreaId = area.Id,
            EmailUsuario = " juan.perez ",
            Firma = "  data:image/png;base64,AAA==  "
        });

        var a = asisRepo.Added.Single();
        Assert.Equal("Juan", a.Nombres);
        Assert.Equal("Perez", a.Apellidos);
        Assert.Equal("1712345678", a.Identificacion);
        Assert.Equal("juan.perez@dos.com.ec", a.EmailUsuario);
        Assert.Equal("data:image/png;base64,AAA==", a.Firma);
    }

    [Fact]
    public async Task ExecuteAsync_Duplicado_LanzaInscripcionDuplicada()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var asisRepo = new FakeAsistenteRepo();
        asisRepo.PreExistingDupes.Add((capacitacion.Id, "1712345678"));

        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion),
            new FakeAreaRepo(area),
            asisRepo);

        await Assert.ThrowsAsync<InscripcionDuplicadaException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id)));

        Assert.Empty(asisRepo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionFinalizada_LanzaInscripcionCerrada()
    {
        var capacitacion = BuildCapacitacion(finalizada: true);
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var asisRepo = new FakeAsistenteRepo();

        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion),
            new FakeAreaRepo(area),
            asisRepo);

        await Assert.ThrowsAsync<InscripcionCerradaException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id)));

        Assert.Empty(asisRepo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_EmailConArroba_LanzaValidacion()
    {
        var capacitacion = BuildCapacitacion();
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var asisRepo = new FakeAsistenteRepo();

        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion),
            new FakeAreaRepo(area),
            asisRepo);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id, emailUsuario: "juan@dos.com.ec")));

        Assert.Equal("EMAIL_INVALIDO", ex.Codigo);
        Assert.Empty(asisRepo.Added);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInactiva_LanzaInactiva()
    {
        var capacitacion = BuildCapacitacion(activo: false);
        var area = new Area { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
        var asisRepo = new FakeAsistenteRepo();

        var useCase = new InscribirAsistenteUseCase(
            new FakeCapacitacionRepo(capacitacion),
            new FakeAreaRepo(area),
            asisRepo);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, BuildInput(area.Id)));

        Assert.Equal("CAPACITACION_INACTIVA", ex.Codigo);
        Assert.Empty(asisRepo.Added);
    }

    // ----- Fakes -----

    private sealed class FakeCapacitacionRepo : ICapacitacionRepository
    {
        private readonly Capacitacion? _entity;
        public FakeCapacitacionRepo(Capacitacion? entity) { _entity = entity; }

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

    private sealed class FakeAreaRepo : IAreaRepository
    {
        private readonly Area? _area;
        public FakeAreaRepo(Area? area) { _area = area; }

        public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_area is not null && _area.Id == id ? _area : null);

        public Task<IEnumerable<Area>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
        {
            if (_area is null) return Task.FromResult(Enumerable.Empty<Area>());
            if (!includeInactive && !_area.Activo) return Task.FromResult(Enumerable.Empty<Area>());
            return Task.FromResult<IEnumerable<Area>>(new[] { _area });
        }

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

    private sealed class FakeAsistenteRepo : IAsistenteRepository
    {
        public List<Asistente> Added { get; } = new();
        public List<(Guid capacitacionId, string identificacion)> PreExistingDupes { get; } = new();

        public FakeAsistenteRepo() { }
        public FakeAsistenteRepo(out FakeAsistenteRepo self)
        {
            self = this;
        }

        public Task AddAsync(Asistente entity, CancellationToken ct = default)
        {
            Added.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Asistente>>(Added.Where(a => a.CapacitacionId == capacitacionId).ToList());

        public Task<bool> ExistsByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default)
        {
            var exists = PreExistingDupes.Any(x => x.capacitacionId == capacitacionId && x.identificacion == identificacion)
                         || Added.Any(a => a.CapacitacionId == capacitacionId && a.Identificacion == identificacion);
            return Task.FromResult(exists);
        }

        public Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Added.FirstOrDefault(a => a.Id == id));
    }
}
