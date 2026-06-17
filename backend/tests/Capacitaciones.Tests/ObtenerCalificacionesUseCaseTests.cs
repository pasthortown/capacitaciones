using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Calificaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso Fase 11 <see cref="ObtenerCalificacionesUseCase"/>.
/// Valida el filtrado a solo <c>Presente</c>, el orden alfabético y la propagación
/// de <c>PuntajeMinimo</c> al DTO.
/// </summary>
public class ObtenerCalificacionesUseCaseTests
{
    private static Capacitacion BuildCapacitacion(
        bool activo = true,
        TipoCertificacion tipo = TipoCertificacion.Aprobacion,
        decimal? puntajeMinimo = 7.0m) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "CAP-PC-REG-011",
        Tema = "Certificado de Aprobación",
        Capacitador = "Ana",
        ModalidadId = Guid.NewGuid(),
        TipoActividadId = Guid.NewGuid(),
        TipoCertificacion = tipo,
        PuntajeMinimo = tipo == TipoCertificacion.Aprobacion ? puntajeMinimo : null,
        FechaHoraInicio = DateTime.UtcNow.AddDays(1),
        DuracionMinutos = 60,
        Activo = activo,
        FechaCreacion = DateTime.UtcNow.AddDays(-2)
    };

    private static Asistente BuildAsistente(
        Guid capacitacionId,
        string nombres,
        string apellidos,
        EstadoAsistencia? estado = EstadoAsistencia.Presente,
        decimal? calificacion = null) => new()
    {
        Id = Guid.NewGuid(),
        CapacitacionId = capacitacionId,
        Nombres = nombres,
        Apellidos = apellidos,
        Identificacion = Guid.NewGuid().ToString("N").Substring(0, 10),
        AreaId = Guid.NewGuid(),
        EmailUsuario = $"{nombres}.{apellidos}@dos.com.ec",
        Firma = "data:image/png;base64,AAA==",
        FechaInscripcion = DateTime.UtcNow,
        EstadoAsistencia = estado,
        FechaMarcacionAsistencia = estado.HasValue ? DateTime.UtcNow.AddMinutes(-5) : null,
        Calificacion = calificacion
    };

    [Fact]
    public async Task ExecuteAsync_FiltraSoloPresentes()
    {
        var cap = BuildCapacitacion();
        var asistentes = new List<Asistente>
        {
            BuildAsistente(cap.Id, "Juan", "Perez", EstadoAsistencia.Presente),
            BuildAsistente(cap.Id, "Ana", "Alvarez", EstadoAsistencia.Ausente),
            BuildAsistente(cap.Id, "Bob", "Brito", estado: null),
            BuildAsistente(cap.Id, "Maria", "Perez", EstadoAsistencia.Presente)
        };

        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(asistentes);
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal(2, dto.Asistentes.Count);
        Assert.All(dto.Asistentes, a => Assert.Equal("Presente", a.EstadoAsistencia));
    }

    [Fact]
    public async Task ExecuteAsync_OrdenaPorApellidosYLuegoNombres()
    {
        var cap = BuildCapacitacion();
        var asistentes = new List<Asistente>
        {
            BuildAsistente(cap.Id, "Luis", "Zuñiga"),
            BuildAsistente(cap.Id, "Maria", "Perez"),
            BuildAsistente(cap.Id, "Ana", "Alvarez"),
            BuildAsistente(cap.Id, "Juan", "Perez")
        };

        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(asistentes);
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal(4, dto.Asistentes.Count);
        Assert.Equal("Alvarez", dto.Asistentes[0].Apellidos);
        Assert.Equal("Juan", dto.Asistentes[1].Nombres);
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
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal("ALPHA", dto.Asistentes[0].Apellidos);
        Assert.Equal("bravo", dto.Asistentes[1].Apellidos);
        Assert.Equal("Charlie", dto.Asistentes[2].Apellidos);
    }

    [Fact]
    public async Task ExecuteAsync_PropagaPuntajeMinimoYTipoCertificacion()
    {
        var cap = BuildCapacitacion(puntajeMinimo: 7.5m);
        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(Array.Empty<Asistente>());
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal("Aprobacion", dto.Capacitacion.TipoCertificacion);
        Assert.Equal(7.5m, dto.Capacitacion.PuntajeMinimo);
    }

    [Fact]
    public async Task ExecuteAsync_PropagaCalificacionExistente()
    {
        var cap = BuildCapacitacion();
        var a1 = BuildAsistente(cap.Id, "Ana", "Alvarez", calificacion: 9.5m);
        var a2 = BuildAsistente(cap.Id, "Bob", "Brito"); // calificacion null

        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(new[] { a1, a2 });
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(cap.Id);

        Assert.Equal(9.5m, dto.Asistentes[0].Calificacion);
        Assert.Null(dto.Asistentes[1].Calificacion);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionDeParticipacion_LanzaCalificacionesNoAplica()
    {
        var cap = BuildCapacitacion(tipo: TipoCertificacion.Participacion);
        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(Array.Empty<Asistente>());
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() => useCase.ExecuteAsync(cap.Id));
        Assert.Equal("CALIFICACIONES_NO_APLICA", ex.Codigo);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInexistente_LanzaNotFound()
    {
        var capRepo = new FakeCapacitacionRepository();
        var asisRepo = new FakeAsistenteRepo(Array.Empty<Asistente>());
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInactiva_LanzaForbidden()
    {
        var cap = BuildCapacitacion(activo: false);
        var capRepo = new FakeCapacitacionRepository(cap);
        var asisRepo = new FakeAsistenteRepo(Array.Empty<Asistente>());
        var useCase = new ObtenerCalificacionesUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitadorForbiddenException>(() => useCase.ExecuteAsync(cap.Id));
    }

    private sealed class FakeCapacitacionRepository : ICapacitacionRepository
    {
        public Task<string?> GetLatestFirmaCapacitadorByNombreAsync(string capacitador, Guid? excluirCapacitacionId = null, CancellationToken ct = default) => Task.FromResult<string?>(null);
        private readonly Capacitacion? _entity;
        public FakeCapacitacionRepository(Capacitacion? entity = null) { _entity = entity; }

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Capacitacion?>(null);
            return Task.FromResult<Capacitacion?>(_entity);
        }

        public Task<IReadOnlyList<Capacitacion>> ListAsync(bool includeInactive = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<string> AddAsync(Capacitacion entity, Func<CancellationToken, Task<string>> codeFactory, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateWithResponsablesAsync(Capacitacion entity, IEnumerable<CapacitacionResponsable> nuevasRelaciones, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Capacitacion entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteLogicoAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeAsistenteRepo : IAsistenteRepository
    {
        // Stubs del flujo de envío de certificados (no ejercitados por estos tests).
        public Task<int> MarcarEstadoEnvioElegiblesAsync(Guid capacitacionId, ISet<Guid> elegibleIds, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> MarcarErroresComoPendientesAsync(Guid capacitacionId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Asistente>> ListByEstadoEnvioAsync(Guid capacitacionId, EstadoEnvioCertificado estado, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Asistente>)new List<Asistente>());
        public Task ActualizarResultadoEnvioAsync(Guid asistenteId, EstadoEnvioCertificado estado, DateTime? fechaEnvio, string? mensajeError, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> ListCapacitacionesConPendientesAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Guid>)new List<Guid>());
        private readonly IList<Asistente> _items;
        public FakeAsistenteRepo(IEnumerable<Asistente> items) { _items = items.ToList(); }

        public Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Asistente>>(_items.Where(a => a.CapacitacionId == capacitacionId).ToList());

        public Task AddAsync(Asistente entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Asistente?> GetByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<bool> ExistsByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(Asistente entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, int>> CountByCapacitacionesAsync(IEnumerable<Guid> capacitacionIds, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
