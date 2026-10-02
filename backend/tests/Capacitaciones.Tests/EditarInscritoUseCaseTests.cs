using Capacitaciones.Application.Dtos.Inscritos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Application.UseCases.Inscritos;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>Tests unitarios de <see cref="EditarInscritoUseCase"/> (pantalla admin "Inscritos").</summary>
public class EditarInscritoUseCaseTests
{
    private const string FirmaVieja = "data:image/png;base64,VIEJA==";
    private const string FirmaNueva = "data:image/png;base64,NUEVA==";

    private static readonly Area AreaTi = new() { Id = Guid.NewGuid(), Nombre = "TI", Activo = true, FechaCreacion = DateTime.UtcNow };
    private static readonly Area AreaRrhh = new() { Id = Guid.NewGuid(), Nombre = "RRHH", Activo = true, FechaCreacion = DateTime.UtcNow };
    private static readonly Area AreaInactiva = new() { Id = Guid.NewGuid(), Nombre = "Vieja", Activo = false, FechaCreacion = DateTime.UtcNow };

    private static Asistente BuildAsistente(string? firma = FirmaVieja) => new()
    {
        Id = Guid.NewGuid(),
        CapacitacionId = Guid.NewGuid(),
        Capacitacion = new Capacitacion { Id = Guid.NewGuid(), Codigo = "CAP-PC-REG-010", Tema = "Excel" },
        Nombres = "Jaun",
        Apellidos = "Peres",
        Identificacion = "1712345678",
        AreaId = AreaTi.Id,
        Area = AreaTi,
        EmailUsuario = "juan.peres@dos.com.ec",
        Firma = firma ?? string.Empty,
        FechaInscripcion = DateTime.UtcNow.AddDays(-3)
    };

    private static EditarInscritoDto BuildInput(string? firma = null, string identificacion = "1712345678", Guid? areaId = null, string email = "juan.perez") => new()
    {
        Nombres = "  Juan ",
        Apellidos = " Pérez ",
        Identificacion = identificacion,
        AreaId = areaId ?? AreaRrhh.Id,
        EmailUsuario = email,
        Firma = firma
    };

    private static (EditarInscritoUseCase useCase, FakeInscritos inscritos, FakePersonas personas) Build(Asistente? asistente, params PersonaInscrita[] personas)
    {
        var repo = new FakeInscritos(asistente);
        var pers = new FakePersonas(personas);
        return (new EditarInscritoUseCase(repo, new FakeAreas(AreaTi, AreaRrhh, AreaInactiva), pers), repo, pers);
    }

    [Fact]
    public async Task HappyPath_ActualizaDatosYConservaFirmaSiNoSeEnviaOtra()
    {
        var asistente = BuildAsistente();
        var (useCase, repo, _) = Build(asistente);

        var dto = await useCase.ExecuteAsync(asistente.Id, BuildInput());

        Assert.Equal("Juan", asistente.Nombres);
        Assert.Equal("Pérez", asistente.Apellidos);
        Assert.Equal(AreaRrhh.Id, asistente.AreaId);
        Assert.Equal("juan.perez@dos.com.ec", asistente.EmailUsuario);
        Assert.Equal(FirmaVieja, asistente.Firma);
        Assert.Equal(1, repo.Saves);
        Assert.Equal("juan.perez", dto.EmailUsuario);
        Assert.Equal("juan.perez@dos.com.ec", dto.Email);
        Assert.Equal("RRHH", dto.AreaNombre);
        Assert.Equal(FirmaVieja, dto.Firma);
        Assert.True(dto.TieneFirma);
        Assert.Equal("CAP-PC-REG-010", dto.CapacitacionCodigo);
    }

    [Fact]
    public async Task FirmaNueva_ReemplazaEnAsistenteYEnPersona()
    {
        var asistente = BuildAsistente();
        var persona = new PersonaInscrita { Id = Guid.NewGuid(), Identificacion = "1712345678", Nombres = "Jaun", Apellidos = "Peres", EmailUsuario = "x@dos.com.ec", Firma = FirmaVieja };
        var (useCase, _, personas) = Build(asistente, persona);

        await useCase.ExecuteAsync(asistente.Id, BuildInput(firma: "  " + FirmaNueva + " "));

        Assert.Equal(FirmaNueva, asistente.Firma);
        Assert.Equal(FirmaNueva, persona.Firma);
        Assert.Equal("Juan", persona.Nombres);
        Assert.Equal("Pérez", persona.Apellidos);
        Assert.Equal(AreaRrhh.Id, persona.AreaId);
        Assert.Equal("juan.perez@dos.com.ec", persona.EmailUsuario);
        Assert.Empty(personas.Agregadas);
    }

    [Fact]
    public async Task SinFirmaNueva_NoPisaLaFirmaDeLaPersona()
    {
        var asistente = BuildAsistente();
        var persona = new PersonaInscrita { Id = Guid.NewGuid(), Identificacion = "1712345678", Nombres = "A", Apellidos = "B", EmailUsuario = "x@dos.com.ec", Firma = "data:image/png;base64,MASRECIENTE==" };
        var (useCase, _, _) = Build(asistente, persona);

        await useCase.ExecuteAsync(asistente.Id, BuildInput());

        Assert.Equal("data:image/png;base64,MASRECIENTE==", persona.Firma);
        Assert.Equal("Juan", persona.Nombres);
    }

    [Fact]
    public async Task PersonaSinFirma_RecibeLaFirmaDelInscrito()
    {
        var asistente = BuildAsistente();
        var persona = new PersonaInscrita { Id = Guid.NewGuid(), Identificacion = "1712345678", Nombres = "A", Apellidos = "B", EmailUsuario = "x@dos.com.ec", Firma = null };
        var (useCase, _, _) = Build(asistente, persona);

        await useCase.ExecuteAsync(asistente.Id, BuildInput());

        Assert.Equal(FirmaVieja, persona.Firma);
    }

    [Fact]
    public async Task PersonaInexistente_SeCreaConLaCedulaFinal()
    {
        var asistente = BuildAsistente();
        var (useCase, _, personas) = Build(asistente);

        await useCase.ExecuteAsync(asistente.Id, BuildInput(identificacion: " 0912345678 "));

        Assert.Equal("0912345678", asistente.Identificacion);
        var p = Assert.Single(personas.Agregadas);
        Assert.Equal("0912345678", p.Identificacion);
        Assert.Equal("Juan", p.Nombres);
        Assert.Equal(FirmaVieja, p.Firma);
        Assert.Equal(AreaRrhh.Id, p.AreaId);
    }

    [Fact]
    public async Task CedulaDuplicadaEnLaCapacitacion_Lanza409YNoGuarda()
    {
        var asistente = BuildAsistente();
        var (useCase, repo, _) = Build(asistente);
        repo.OtrasIdentificaciones.Add("0999999999");

        await Assert.ThrowsAsync<InscripcionDuplicadaException>(() =>
            useCase.ExecuteAsync(asistente.Id, BuildInput(identificacion: "0999999999")));

        Assert.Equal("1712345678", asistente.Identificacion);
        Assert.Equal("Jaun", asistente.Nombres);
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task AreaInactiva_LanzaAreaInvalida()
    {
        var asistente = BuildAsistente();
        var (useCase, repo, _) = Build(asistente);

        await Assert.ThrowsAsync<InscripcionAreaInvalidaException>(() =>
            useCase.ExecuteAsync(asistente.Id, BuildInput(areaId: AreaInactiva.Id)));
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task EmailConArroba_LanzaEmailInvalido()
    {
        var asistente = BuildAsistente();
        var (useCase, repo, _) = Build(asistente);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(asistente.Id, BuildInput(email: "juan@dos.com.ec")));
        Assert.Equal("EMAIL_INVALIDO", ex.Codigo);
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task NombresVacios_LanzaCampoRequerido()
    {
        var asistente = BuildAsistente();
        var (useCase, _, _) = Build(asistente);
        var input = BuildInput();
        input.Nombres = "   ";

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() => useCase.ExecuteAsync(asistente.Id, input));
        Assert.Equal("CAMPO_REQUERIDO", ex.Codigo);
    }

    [Fact]
    public async Task InscritoInexistente_LanzaNotFound()
    {
        var (useCase, _, _) = Build(null);

        await Assert.ThrowsAsync<InscritoNotFoundException>(() => useCase.ExecuteAsync(Guid.NewGuid(), BuildInput()));
    }

    // ----- Fakes -----

    private sealed class FakeInscritos : IInscritoRepository
    {
        private readonly Asistente? _asistente;
        public HashSet<string> OtrasIdentificaciones { get; } = new();
        public int Saves { get; private set; }

        public FakeInscritos(Asistente? asistente) { _asistente = asistente; }

        public Task<IReadOnlyList<InscritoResumen>> ListAsync(Guid? capacitacionId, string? buscar, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<Asistente?> GetForEditAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_asistente is not null && _asistente.Id == id ? _asistente : null);

        public Task<bool> ExistsOtroConIdentificacionAsync(Guid capacitacionId, string identificacion, Guid excluirId, CancellationToken ct = default)
            => Task.FromResult(OtrasIdentificaciones.Contains(identificacion));

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePersonas : IPersonaInscritaRepository
    {
        private readonly Dictionary<string, PersonaInscrita> _store = new();
        public List<PersonaInscrita> Agregadas { get; } = new();

        public FakePersonas(IEnumerable<PersonaInscrita> existentes)
        {
            foreach (var p in existentes) _store[p.Identificacion] = p;
        }

        public Task<PersonaInscrita?> GetByIdentificacionAsync(string identificacion, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(identificacion, out var p) ? p : null);

        public void Agregar(PersonaInscrita entity)
        {
            Agregadas.Add(entity);
            _store[entity.Identificacion] = entity;
        }
    }

    private sealed class FakeAreas : IAreaRepository
    {
        private readonly Area[] _areas;
        public FakeAreas(params Area[] areas) { _areas = areas; }
        public Task<Area?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_areas.FirstOrDefault(a => a.Id == id));
        public Task<IEnumerable<Area>> ListAsync(bool includeInactive = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Area?> GetByNombreAsync(string nombre, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Area entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Area> entities, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Area entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
