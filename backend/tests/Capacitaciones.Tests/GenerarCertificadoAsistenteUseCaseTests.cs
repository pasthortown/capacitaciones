using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Certificados;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios Fase 6 de <see cref="GenerarCertificadoAsistenteUseCase"/>.
/// Usan fakes manuales siguiendo el estilo del resto del proyecto — ningún HttpClient real.
/// </summary>
public class GenerarCertificadoAsistenteUseCaseTests
{
    private static readonly Guid CapacitacionId = Guid.NewGuid();
    private static readonly Guid AsistenteId = Guid.NewGuid();

    private static Capacitacion BuildCapacitacion(
        bool finalizada = true,
        bool capacitadorFirma = true,
        int responsableCount = 2,
        bool responsablesTienenFirma = true)
    {
        // Para "Finalizada" elegimos una fecha de inicio hace 2h con duración de 1h.
        var inicio = finalizada
            ? DateTime.UtcNow.AddHours(-2)
            : DateTime.UtcNow.AddDays(7);

        var cap = new Capacitacion
        {
            Id = CapacitacionId,
            Codigo = "CAP-PC-REG-001",
            Tema = "Ciberseguridad",
            Capacitador = "Pedro Pérez",
            CargoCapacitador = "Capacitador",
            EmpresaCapacitador = "DOS",
            FirmaCapacitador = capacitadorFirma ? "data:image/png;base64,FIRMA_CAP" : null,
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = inicio,
            DuracionMinutos = 60,
            Activo = true,
            TipoActividad = new TipoActividad
            {
                Id = Guid.NewGuid(),
                Nombre = "Curso",
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            },
            Modalidad = new Modalidad
            {
                Id = Guid.NewGuid(),
                Nombre = "Virtual",
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            }
        };

        // Agregamos responsables en orden INVERSO para verificar que el useCase los ordena por Orden ASC.
        for (int i = responsableCount - 1; i >= 0; i--)
        {
            var responsable = new Responsable
            {
                Id = Guid.NewGuid(),
                Nombres = $"Responsable {i + 1}",
                Cargo = $"Cargo {i + 1}",
                Empresa = "DOS",
                Firma = responsablesTienenFirma ? $"data:image/png;base64,FIRMA_R{i + 1}" : null,
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            cap.CapacitacionResponsables.Add(new CapacitacionResponsable
            {
                CapacitacionId = cap.Id,
                ResponsableId = responsable.Id,
                Responsable = responsable,
                Orden = i
            });
        }

        return cap;
    }

    private static Asistente BuildAsistente(Guid capacitacionId) => new()
    {
        Id = AsistenteId,
        CapacitacionId = capacitacionId,
        Nombres = "Luis Alfonso",
        Apellidos = "Salazar Vaca",
        Identificacion = "1712345678",
        AreaId = Guid.NewGuid(),
        EmailUsuario = "lsalazar@dos.com.ec",
        Firma = "data:image/png;base64,FIRMA_ASISTENTE",
        FechaInscripcion = DateTime.UtcNow.AddDays(-1),
        // Fase 12 — los certificados exigen EstadoAsistencia = Presente; los tests de Fase 6
        // se crearon antes de esta regla, así que lo dejamos en Presente por defecto.
        EstadoAsistencia = EstadoAsistencia.Presente,
        FechaMarcacionAsistencia = DateTime.UtcNow.AddMinutes(-10)
    };

    [Fact]
    public async Task ExecuteAsync_HappyPath_LlamaAEmisorConPayloadCorrectoYOrdenDeFirmantes()
    {
        var cap = BuildCapacitacion();
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient("/output/CAP-PC-REG-001_1712345678.pdf");
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var result = await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Equal("/output/CAP-PC-REG-001_1712345678.pdf", result.Ruta);
        Assert.Equal("CAP-PC-REG-001_1712345678.pdf", result.Filename);

        // Verificamos payload.
        Assert.NotNull(emisor.LastRequest);
        var req = emisor.LastRequest!;
        Assert.Equal("CAP-PC-REG-001", req.Capacitacion.Codigo);
        Assert.Equal("Ciberseguridad", req.Capacitacion.Tema);
        Assert.Equal("Curso", req.Capacitacion.TipoActividad);
        Assert.Equal("Participacion", req.Capacitacion.TipoCertificacion);
        Assert.EndsWith("Z", req.Capacitacion.FechaInicio); // ISO UTC con Z.
        Assert.Equal(1m, req.Capacitacion.DuracionHoras);  // 60 min / 60 = 1

        Assert.Equal("Luis Alfonso", req.Asistente.Nombres);
        Assert.Equal("Salazar Vaca", req.Asistente.Apellidos);
        Assert.Equal("1712345678", req.Asistente.Identificacion);

        // Firmantes: capacitador primero, luego responsables en Orden ASC (0, 1).
        Assert.Equal(3, req.Firmantes.Count);
        Assert.Equal("Pedro Pérez", req.Firmantes[0].Nombres);
        Assert.Equal("data:image/png;base64,FIRMA_CAP", req.Firmantes[0].FirmaBase64);
        Assert.Equal("Responsable 1", req.Firmantes[1].Nombres); // Orden=0 → Responsable 1 (loop invertido)
        Assert.Equal("Responsable 2", req.Firmantes[2].Nombres); // Orden=1 → Responsable 2
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionNoFinalizada_LanzaCertificadoNoDisponible()
    {
        var cap = BuildCapacitacion(finalizada: false);
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var ex = await Assert.ThrowsAsync<CertificadoNoDisponibleException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));

        Assert.Equal("CAPACITACION_NO_FINALIZADA", ex.Codigo);
        Assert.Null(emisor.LastRequest); // no se llamó al emisor
    }

    [Fact]
    public async Task ExecuteAsync_CapacitadorSinFirma_LanzaFirmasFaltantes()
    {
        var cap = BuildCapacitacion(capacitadorFirma: false, responsableCount: 0);
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var ex = await Assert.ThrowsAsync<CertificadoFirmasFaltantesException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));

        Assert.Equal("FIRMAS_FALTANTES", ex.Codigo);
        Assert.Contains(ex.Faltantes, f => f.Contains("Pedro", StringComparison.OrdinalIgnoreCase));
        Assert.Null(emisor.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ResponsableSinFirma_LanzaFirmasFaltantesConNombre()
    {
        var cap = BuildCapacitacion(responsableCount: 1, responsablesTienenFirma: false);
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var ex = await Assert.ThrowsAsync<CertificadoFirmasFaltantesException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));

        Assert.Equal("FIRMAS_FALTANTES", ex.Codigo);
        Assert.Contains("Responsable 1", ex.Faltantes);
        Assert.Null(emisor.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_AsistenteDeOtraCapacitacion_Lanza404()
    {
        var cap = BuildCapacitacion();
        var asistente = BuildAsistente(capacitacionId: Guid.NewGuid()); // otro capacitacionId
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));

        Assert.Equal("ASISTENTE_NOT_FOUND", ex.Codigo);
        Assert.Null(emisor.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_CapacitacionInexistente_LanzaNotFound()
    {
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(null),
            new FakeAsistenteRepo(null),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));
    }

    // ---------- Fase 12 — lógica condicional de certificado ----------

    [Fact]
    public async Task ExecuteAsync_AsistenteAusente_LanzaNoElegibleConMotivoAusente()
    {
        var cap = BuildCapacitacion();
        var asistente = BuildAsistente(CapacitacionId);
        asistente.EstadoAsistencia = EstadoAsistencia.Ausente;
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var ex = await Assert.ThrowsAsync<CertificadoAsistenteNoElegibleException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));
        Assert.Equal("ASISTENTE_NO_ELEGIBLE_CERTIFICADO", ex.Codigo);
        Assert.Equal("AUSENTE", ex.Motivo);
        Assert.Null(emisor.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_AsistenteSinMarcar_LanzaNoElegibleConMotivoSinMarcar()
    {
        var cap = BuildCapacitacion();
        var asistente = BuildAsistente(CapacitacionId);
        asistente.EstadoAsistencia = null;
        asistente.FechaMarcacionAsistencia = null;
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        var ex = await Assert.ThrowsAsync<CertificadoAsistenteNoElegibleException>(
            () => useCase.ExecuteAsync(CapacitacionId, AsistenteId));
        Assert.Equal("SIN_MARCAR", ex.Motivo);
        Assert.Null(emisor.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_Participacion_CertificadoEfectivoEsParticipacion()
    {
        var cap = BuildCapacitacion(); // tipo default = Participacion
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.NotNull(emisor.LastRequest);
        Assert.Equal("Participacion", emisor.LastRequest!.CertificadoEfectivo);
        Assert.Null(emisor.LastRequest.Capacitacion.PuntajeMinimo);
        Assert.Null(emisor.LastRequest.Asistente.Calificacion);
    }

    [Fact]
    public async Task ExecuteAsync_AprobacionConCalificacionSuficiente_CertificadoEfectivoEsAprobacion()
    {
        var cap = BuildCapacitacion();
        cap.TipoCertificacion = TipoCertificacion.Aprobacion;
        cap.PuntajeMinimo = 7.0m;
        var asistente = BuildAsistente(CapacitacionId);
        asistente.Calificacion = 8.5m;
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Equal("Aprobacion", emisor.LastRequest!.CertificadoEfectivo);
        Assert.Equal(7.0m, emisor.LastRequest.Capacitacion.PuntajeMinimo);
        Assert.Equal(8.5m, emisor.LastRequest.Asistente.Calificacion);
    }

    [Fact]
    public async Task ExecuteAsync_AprobacionJustoEnElLimite_CertificadoEfectivoEsAprobacion()
    {
        // Borde: calificación == puntaje mínimo. La regla es `>=`, así que aprueba.
        var cap = BuildCapacitacion();
        cap.TipoCertificacion = TipoCertificacion.Aprobacion;
        cap.PuntajeMinimo = 7.0m;
        var asistente = BuildAsistente(CapacitacionId);
        asistente.Calificacion = 7.0m;
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Equal("Aprobacion", emisor.LastRequest!.CertificadoEfectivo);
    }

    [Fact]
    public async Task ExecuteAsync_AprobacionCalificacionInsuficiente_CertificadoEfectivoEsAsistencia()
    {
        var cap = BuildCapacitacion();
        cap.TipoCertificacion = TipoCertificacion.Aprobacion;
        cap.PuntajeMinimo = 7.0m;
        var asistente = BuildAsistente(CapacitacionId);
        asistente.Calificacion = 6.9m;
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Equal("Asistencia", emisor.LastRequest!.CertificadoEfectivo);
        Assert.Equal(6.9m, emisor.LastRequest.Asistente.Calificacion);
    }

    [Fact]
    public async Task ExecuteAsync_AprobacionSinCalificacion_CertificadoEfectivoEsAsistencia()
    {
        // Aprobacion + Presente pero calificacion = null ⇒ fallback seguro "Asistencia".
        var cap = BuildCapacitacion();
        cap.TipoCertificacion = TipoCertificacion.Aprobacion;
        cap.PuntajeMinimo = 7.0m;
        var asistente = BuildAsistente(CapacitacionId);
        asistente.Calificacion = null;
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Equal("Asistencia", emisor.LastRequest!.CertificadoEfectivo);
        Assert.Null(emisor.LastRequest.Asistente.Calificacion);
    }

    [Fact]
    public async Task ExecuteAsync_ConLogo_IncluyeLogoPathLocalEnPayload()
    {
        var cap = BuildCapacitacion();
        cap.LogoPath = "abc123.png";
        cap.LogoContentType = "image/png";
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Equal("/imagen_capacitaciones/abc123.png", emisor.LastRequest!.Capacitacion.LogoPathLocal);
    }

    [Fact]
    public async Task ExecuteAsync_SinLogo_LogoPathLocalEsNull()
    {
        var cap = BuildCapacitacion();
        cap.LogoPath = null;
        var asistente = BuildAsistente(CapacitacionId);
        var emisor = new FakeEmisorDocumentosClient();
        var useCase = new GenerarCertificadoAsistenteUseCase(
            new FakeCapacitacionRepo(cap),
            new FakeAsistenteRepo(asistente),
            emisor,
            new Capacitaciones.Application.UseCases.Asistentes.CertificadosOptions());

        await useCase.ExecuteAsync(CapacitacionId, AsistenteId);

        Assert.Null(emisor.LastRequest!.Capacitacion.LogoPathLocal);
    }

    // ---------- Fakes ----------

    private sealed class FakeEmisorDocumentosClient : IEmisorDocumentosClient
    {
        private readonly string _ruta;

        public FakeEmisorDocumentosClient(string ruta = "/output/fake.pdf")
        {
            _ruta = ruta;
        }

        public EmisionRequest? LastRequest { get; private set; }
        public int EmitirCallCount { get; private set; }

        public Task<EmisionResultado> EmitirReporteAsistenciaAsync(ReporteAsistenciaRequest req, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<EmisionResultado> EmitirConvenioAsync(Capacitaciones.Application.Dtos.Convenios.ConvenioImprimirRequest req, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<EmisionResultado> EmitirReporteConveniosAsync(Capacitaciones.Application.Dtos.Convenios.ReporteConveniosRequest req, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<EmisionResultado> EmitirDashboardConveniosAsync(Capacitaciones.Application.Dtos.Convenios.DashboardConveniosRequest req, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<EmisionResultado> EmitirAsync(EmisionRequest req, CancellationToken ct)
        {
            LastRequest = req;
            EmitirCallCount++;
            return Task.FromResult(new EmisionResultado { Ruta = _ruta });
        }

        public Task<bool> IsHealthyAsync(CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeCapacitacionRepo : ICapacitacionRepository
    {
        public Task<string?> GetLatestFirmaCapacitadorByNombreAsync(string capacitador, Guid? excluirCapacitacionId = null, CancellationToken ct = default) => Task.FromResult<string?>(null);
        private readonly Capacitacion? _entity;

        public FakeCapacitacionRepo(Capacitacion? entity)
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

    private sealed class FakeAsistenteRepo : IAsistenteRepository
    {
        // Stubs del flujo de envío de certificados (no ejercitados por estos tests).
        public Task<int> MarcarEstadoEnvioElegiblesAsync(Guid capacitacionId, ISet<Guid> elegibleIds, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> MarcarErroresComoPendientesAsync(Guid capacitacionId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<Asistente>> ListByEstadoEnvioAsync(Guid capacitacionId, EstadoEnvioCertificado estado, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Asistente>)new List<Asistente>());
        public Task ActualizarResultadoEnvioAsync(Guid asistenteId, EstadoEnvioCertificado estado, DateTime? fechaEnvio, string? mensajeError, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> ListCapacitacionesConPendientesAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Guid>)new List<Guid>());
        private readonly Asistente? _entity;

        public FakeAsistenteRepo(Asistente? entity)
        {
            _entity = entity;
        }

        public Task<Asistente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (_entity is null || _entity.Id != id) return Task.FromResult<Asistente?>(null);
            return Task.FromResult<Asistente?>(_entity);
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
        public Task UpdateAsync(Asistente entity, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
