using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Calificaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.PaseLista;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso Fase 11 <see cref="CalificarAsistenteUseCase"/>.
/// Cubren: happy path (presente + calificación válida), limpieza (null), rechazos por
/// tipo de certificación, estado de asistencia, rango, mismatch capacitación-asistente,
/// capacitación inexistente/inactiva.
/// </summary>
public class CalificarAsistenteUseCaseTests
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
        EstadoAsistencia? estado = EstadoAsistencia.Presente) => new()
    {
        Id = Guid.NewGuid(),
        CapacitacionId = capacitacionId,
        Nombres = "Juan",
        Apellidos = "Perez",
        Identificacion = "1712345678",
        AreaId = Guid.NewGuid(),
        EmailUsuario = "juan.perez@dos.com.ec",
        Firma = "data:image/png;base64,AAA==",
        FechaInscripcion = DateTime.UtcNow,
        EstadoAsistencia = estado,
        FechaMarcacionAsistencia = estado.HasValue ? DateTime.UtcNow.AddMinutes(-5) : null
    };

    [Fact]
    public async Task ExecuteAsync_PresenteYCalificacionValida_Persiste()
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(capacitacion.Id, asistente.Id, 8.5m);

        Assert.Equal(asistente.Id, dto.Id);
        Assert.Equal(8.5m, dto.Calificacion);
        Assert.Equal(8.5m, asistente.Calificacion);
        Assert.Equal(1, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CalificacionNull_LimpiaValor()
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id);
        asistente.Calificacion = 9m;

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(capacitacion.Id, asistente.Id, null);

        Assert.Null(dto.Calificacion);
        Assert.Null(asistente.Calificacion);
        Assert.Equal(1, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionDeParticipacion_LanzaCalificacionesNoAplica()
    {
        var capacitacion = BuildCapacitacion(tipo: TipoCertificacion.Participacion);
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, 8m));
        Assert.Equal("CALIFICACIONES_NO_APLICA", ex.Codigo);
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Theory]
    [InlineData(EstadoAsistencia.Ausente)]
    [InlineData(null)]
    public async Task ExecuteAsync_AsistenteNoPresente_LanzaAsistenteNoPresente(EstadoAsistencia? estado)
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id, estado: estado);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, 7m));
        Assert.Equal("ASISTENTE_NO_PRESENTE", ex.Codigo);
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    [InlineData(-100)]
    [InlineData(999)]
    public async Task ExecuteAsync_CalificacionFueraDeRango_Lanza(decimal calificacion)
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, calificacion));
        Assert.Equal("CALIFICACION_FUERA_DE_RANGO", ex.Codigo);
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(5.5)]
    public async Task ExecuteAsync_CalificacionEnBordesDelRango_Persiste(decimal calificacion)
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(capacitacion.Id, asistente.Id, calificacion);
        Assert.Equal(calificacion, dto.Calificacion);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInexistente_LanzaNotFound()
    {
        var capRepo = new FakeCapacitacionRepository();
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), 7m));
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInactiva_LanzaForbidden()
    {
        var capacitacion = BuildCapacitacion(activo: false);
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitadorForbiddenException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, 7m));
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AsistenteDeOtraCapacitacion_LanzaAsistenteNotFound()
    {
        // Defensa en profundidad: un token Calificaciones con cid=X no puede calificar
        // asistentes de otra capacitación Y aunque el controller público haya dejado pasar.
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(Guid.NewGuid()); // pertenece a otra.

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        var ex = await Assert.ThrowsAsync<AsistenteNotFoundException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, 7m));
        Assert.Equal("ASISTENTE_NOT_FOUND", ex.Codigo);
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AsistenteInexistente_LanzaAsistenteNotFound()
    {
        var capacitacion = BuildCapacitacion();
        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new CalificarAsistenteUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<AsistenteNotFoundException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, Guid.NewGuid(), 7m));
    }

    /// <summary>Stub manual de <see cref="ICapacitacionRepository"/>.</summary>
    private sealed class FakeCapacitacionRepository : ICapacitacionRepository
    {
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

    /// <summary>Stub manual de <see cref="IAsistenteRepository"/>; solo <c>GetByIdAsync</c>/<c>UpdateAsync</c>.</summary>
    private sealed class FakeAsistenteRepo : IAsistenteRepository
    {
        private readonly Asistente? _entity;
        public int UpdateCallCount { get; private set; }

        public FakeAsistenteRepo(Asistente? entity = null) { _entity = entity; }

        public Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Asistente?>(null);
            return Task.FromResult<Asistente?>(_entity);
        }

        public Task UpdateAsync(Asistente entity, CancellationToken ct = default)
        {
            UpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task AddAsync(Asistente entity, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ExistsByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, int>> CountByCapacitacionesAsync(IEnumerable<Guid> capacitacionIds, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
