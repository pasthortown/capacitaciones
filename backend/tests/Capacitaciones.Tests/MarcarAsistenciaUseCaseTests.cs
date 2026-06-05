using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.PaseLista;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios del caso de uso Fase 10 <see cref="MarcarAsistenciaUseCase"/>.
/// Usa fakes manuales en la línea de <see cref="ActualizarCapacitadorCapacitacionUseCaseTests"/>.
/// Validan: happy path (presente/ausente/null), mismatch capacitación-asistente,
/// capacitación inexistente/inactiva y el parseo del string.
/// </summary>
public class MarcarAsistenciaUseCaseTests
{
    private static Capacitacion BuildCapacitacion(bool activo = true) => new()
    {
        Id = Guid.NewGuid(),
        Codigo = "CAP-PC-REG-010",
        Tema = "Seguridad Informática",
        Capacitador = "Ana",
        ModalidadId = Guid.NewGuid(),
        TipoActividadId = Guid.NewGuid(),
        TipoCertificacion = TipoCertificacion.Participacion,
        FechaHoraInicio = DateTime.UtcNow.AddDays(1),
        DuracionMinutos = 60,
        Activo = activo,
        FechaCreacion = DateTime.UtcNow.AddDays(-2)
    };

    private static Asistente BuildAsistente(Guid capacitacionId) => new()
    {
        Id = Guid.NewGuid(),
        CapacitacionId = capacitacionId,
        Nombres = "Juan",
        Apellidos = "Perez",
        Identificacion = "1712345678",
        AreaId = Guid.NewGuid(),
        EmailUsuario = "juan.perez@dos.com.ec",
        Firma = "data:image/png;base64,AAA==",
        FechaInscripcion = DateTime.UtcNow
    };

    [Fact]
    public async Task ExecuteAsync_MarcaPresente_AsignaEstadoYFecha()
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new MarcarAsistenciaUseCase(capRepo, asisRepo);

        var antesUtc = DateTime.UtcNow;
        var dto = await useCase.ExecuteAsync(capacitacion.Id, asistente.Id, EstadoAsistencia.Presente);

        Assert.Equal("Presente", dto.EstadoAsistencia);
        Assert.NotNull(dto.FechaMarcacionAsistencia);
        Assert.True(dto.FechaMarcacionAsistencia >= antesUtc);
        Assert.Equal(EstadoAsistencia.Presente, asistente.EstadoAsistencia);
        Assert.Equal(1, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MarcaNull_LimpiaEstadoYFecha()
    {
        var capacitacion = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacion.Id);
        asistente.EstadoAsistencia = EstadoAsistencia.Ausente;
        asistente.FechaMarcacionAsistencia = DateTime.UtcNow.AddMinutes(-5);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new MarcarAsistenciaUseCase(capRepo, asisRepo);

        var dto = await useCase.ExecuteAsync(capacitacion.Id, asistente.Id, null);

        Assert.Null(dto.EstadoAsistencia);
        Assert.Null(dto.FechaMarcacionAsistencia);
        Assert.Null(asistente.EstadoAsistencia);
        Assert.Null(asistente.FechaMarcacionAsistencia);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInexistente_LanzaNotFound()
    {
        var capRepo = new FakeCapacitacionRepository();
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new MarcarAsistenciaUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), EstadoAsistencia.Presente));
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInactiva_LanzaForbidden()
    {
        var capacitacion = BuildCapacitacion(activo: false);
        var asistente = BuildAsistente(capacitacion.Id);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new MarcarAsistenciaUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<CapacitadorForbiddenException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, EstadoAsistencia.Ausente));
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AsistenteDeOtraCapacitacion_LanzaAsistenteNotFound()
    {
        // Asistente pertenece a otra capacitación — defensa en profundidad: un token PaseLista
        // con cid=X no puede tocar asistentes de Y aunque se envíe el id correcto.
        var capacitacion = BuildCapacitacion();
        var otraCapacitacionId = Guid.NewGuid();
        var asistente = BuildAsistente(otraCapacitacionId);

        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo(asistente);
        var useCase = new MarcarAsistenciaUseCase(capRepo, asisRepo);

        var ex = await Assert.ThrowsAsync<AsistenteNotFoundException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, asistente.Id, EstadoAsistencia.Presente));
        Assert.Equal("ASISTENTE_NOT_FOUND", ex.Codigo);
        Assert.Equal(0, asisRepo.UpdateCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AsistenteInexistente_LanzaAsistenteNotFound()
    {
        var capacitacion = BuildCapacitacion();
        var capRepo = new FakeCapacitacionRepository(capacitacion);
        var asisRepo = new FakeAsistenteRepo();
        var useCase = new MarcarAsistenciaUseCase(capRepo, asisRepo);

        await Assert.ThrowsAsync<AsistenteNotFoundException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, Guid.NewGuid(), EstadoAsistencia.Presente));
    }

    [Theory]
    [InlineData("Presente", EstadoAsistencia.Presente)]
    [InlineData("presente", EstadoAsistencia.Presente)]
    [InlineData("AUSENTE", EstadoAsistencia.Ausente)]
    [InlineData("  Presente  ", EstadoAsistencia.Presente)]
    public void ParseEstado_ValoresValidos_Retorna(string raw, EstadoAsistencia expected)
    {
        var parsed = MarcarAsistenciaUseCase.ParseEstado(raw);
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseEstado_NullOWhitespace_RetornaNull(string? raw)
    {
        Assert.Null(MarcarAsistenciaUseCase.ParseEstado(raw));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0")]
    [InlineData("1")]
    public void ParseEstado_ValoresInvalidos_LanzaExcepcion(string raw)
    {
        var ex = Assert.Throws<CapacitacionServiceException>(() => MarcarAsistenciaUseCase.ParseEstado(raw));
        Assert.Equal("ESTADO_ASISTENCIA_INVALIDO", ex.Codigo);
    }

    /// <summary>
    /// Stub manual de <see cref="ICapacitacionRepository"/>. Solo implementa
    /// <c>GetByIdWithResponsablesAsync</c>; los demás métodos no deberían invocarse.
    /// </summary>
    private sealed class FakeCapacitacionRepository : ICapacitacionRepository
    {
        private readonly Capacitacion? _entity;

        public FakeCapacitacionRepository(Capacitacion? entity = null)
        {
            _entity = entity;
        }

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Capacitacion?>(null);
            return Task.FromResult<Capacitacion?>(_entity);
        }

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

    /// <summary>
    /// Stub manual de <see cref="IAsistenteRepository"/>; solo implementa
    /// <c>GetByIdAsync</c> y <c>UpdateAsync</c> que son los que consume el caso de uso.
    /// </summary>
    private sealed class FakeAsistenteRepo : IAsistenteRepository
    {
        private readonly Asistente? _entity;
        public int UpdateCallCount { get; private set; }

        public FakeAsistenteRepo(Asistente? entity = null)
        {
            _entity = entity;
        }

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

        public Task AddAsync(Asistente entity, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<Asistente>> ListByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<Asistente?> GetByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<bool> ExistsByCapacitacionAndIdentificacionAsync(Guid capacitacionId, string identificacion, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<int> CountByCapacitacionAsync(Guid capacitacionId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyDictionary<Guid, int>> CountByCapacitacionesAsync(IEnumerable<Guid> capacitacionIds, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
