using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Certificados;

/// <summary>
/// Caso de uso Fase 6: arma el payload del certificado de un asistente y lo envía al
/// servicio externo <c>emisor_documentos</c> vía <see cref="IEmisorDocumentosClient"/>.
///
/// Precondiciones:
///   - La capacitación existe (o 404) y su estado calculado es <c>Finalizada</c>.
///   - El asistente existe y pertenece a la capacitación (o 404).
///   - Todos los firmantes tienen firma: capacitador (<c>Capacitacion.FirmaCapacitador</c>)
///     y cada responsable linkeado vía la pivote (<c>Responsable.Firma</c>).
///
/// El orden de los firmantes en el payload respeta la regla de negocio:
///   1. Capacitador (siempre primero).
///   2. Responsables en el <c>Orden</c> configurado (ASC).
/// </summary>
public class GenerarCertificadoAsistenteUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IEmisorDocumentosClient _emisor;

    public GenerarCertificadoAsistenteUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        IEmisorDocumentosClient emisor)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _emisor = emisor;
    }

    public async Task<CertificadoEmitidoDto> ExecuteAsync(
        Guid capacitacionId,
        Guid asistenteId,
        CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var asistente = await _asistentes.GetByIdAsync(asistenteId, ct);
        if (asistente is null || asistente.CapacitacionId != capacitacionId)
        {
            throw new CapacitacionServiceException(
                "ASISTENTE_NOT_FOUND",
                $"No existe un asistente con Id={asistenteId} para la capacitación {capacitacionId}.");
        }

        if (CapacitacionEstadoCalculator.Calcular(capacitacion) != CapacitacionEstadoCalculator.Finalizada)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoFinalizada();
        }

        // Fase 12 — validación de elegibilidad por asistencia (aplica a todos los tipos de certificación).
        // Decisión 10: ausente o sin marcar ⇒ sin certificado, punto.
        if (asistente.EstadoAsistencia == EstadoAsistencia.Ausente)
        {
            throw CertificadoAsistenteNoElegibleException.Ausente();
        }
        if (asistente.EstadoAsistencia is null)
        {
            throw CertificadoAsistenteNoElegibleException.SinMarcar();
        }

        // Validación de firmas: recolectamos todas las faltantes para que el UI pueda mostrarlas
        // de una sola vez en vez de obligar al admin a intentar varias veces.
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(capacitacion.FirmaCapacitador))
        {
            faltantes.Add(string.IsNullOrWhiteSpace(capacitacion.Capacitador)
                ? "Capacitador"
                : capacitacion.Capacitador);
        }

        var responsablesOrdenados = capacitacion.CapacitacionResponsables
            .OrderBy(cr => cr.Orden)
            .ToList();

        foreach (var cr in responsablesOrdenados)
        {
            var resp = cr.Responsable;
            if (resp is null)
            {
                // Defensivo: si el ThenInclude no trajo la entidad, lo reportamos como faltante.
                faltantes.Add($"Responsable {cr.ResponsableId}");
                continue;
            }
            if (string.IsNullOrWhiteSpace(resp.Firma))
            {
                faltantes.Add(string.IsNullOrWhiteSpace(resp.Nombres) ? "Responsable" : resp.Nombres);
            }
        }

        if (faltantes.Count > 0)
        {
            throw new CertificadoFirmasFaltantesException(faltantes);
        }

        var payload = BuildPayload(capacitacion, asistente, responsablesOrdenados);
        var resultado = await _emisor.EmitirAsync(payload, ct);

        var filename = ExtractFilename(resultado.Ruta);
        return new CertificadoEmitidoDto
        {
            Ruta = resultado.Ruta,
            Filename = filename
        };
    }

    private static EmisionRequest BuildPayload(
        Capacitacion capacitacion,
        Asistente asistente,
        IReadOnlyList<CapacitacionResponsable> responsablesOrdenados)
    {
        var firmantes = new List<EmisionFirmanteDto>(responsablesOrdenados.Count + 1)
        {
            new EmisionFirmanteDto
            {
                Nombres = capacitacion.Capacitador ?? string.Empty,
                Cargo = capacitacion.CargoCapacitador ?? string.Empty,
                Empresa = capacitacion.EmpresaCapacitador ?? string.Empty,
                FirmaBase64 = capacitacion.FirmaCapacitador ?? string.Empty
            }
        };

        foreach (var cr in responsablesOrdenados)
        {
            var r = cr.Responsable!;
            firmantes.Add(new EmisionFirmanteDto
            {
                Nombres = r.Nombres,
                Cargo = r.Cargo,
                Empresa = r.Empresa,
                FirmaBase64 = r.Firma ?? string.Empty
            });
        }

        // Normalizamos la fecha a ISO-8601 UTC con Z. Se asume que FechaHoraInicio se persiste en UTC
        // (ver CapacitacionEstadoCalculator: la comparación usa DateTime.UtcNow directamente).
        var fechaUtc = DateTime.SpecifyKind(capacitacion.FechaHoraInicio, DateTimeKind.Utc);
        var fechaIso = fechaUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);

        // DuracionHoras en decimal para permitir medias horas (step de 30 min en UI → 0.5h).
        var duracionHoras = capacitacion.DuracionMinutos / 60m;

        // Fase 12 — ruta local para el emisor. El backend conoce solo el nombre físico
        // (<guid>.ext) en LogoPath; el emisor tiene el volumen montado en /imagen_capacitaciones.
        string? logoPathLocal = null;
        if (!string.IsNullOrWhiteSpace(capacitacion.LogoPath))
        {
            // Usa / explícitamente (Linux en emisor) — ambos contenedores son linux.
            logoPathLocal = $"/imagen_capacitaciones/{capacitacion.LogoPath}";
        }

        return new EmisionRequest
        {
            Capacitacion = new EmisionCapacitacionDto
            {
                Codigo = capacitacion.Codigo,
                Tema = capacitacion.Tema,
                TipoActividad = capacitacion.TipoActividad?.Nombre ?? string.Empty,
                TipoCertificacion = capacitacion.TipoCertificacion.ToString(),
                FechaInicio = fechaIso,
                DuracionHoras = duracionHoras,
                PuntajeMinimo = capacitacion.PuntajeMinimo,
                LogoPathLocal = logoPathLocal
            },
            Asistente = new EmisionAsistenteDto
            {
                Nombres = asistente.Nombres,
                Apellidos = asistente.Apellidos,
                Identificacion = asistente.Identificacion,
                Calificacion = asistente.Calificacion
            },
            Firmantes = firmantes,
            CertificadoEfectivo = CalcularCertificadoEfectivo(capacitacion, asistente)
        };
    }

    /// <summary>
    /// Fase 12 — decide qué etiqueta imprime el certificado. Asume que el asistente ya pasó
    /// la verificación de <see cref="EstadoAsistencia.Presente"/> (cualquier otro estado aborta antes).
    ///
    /// - Participacion + Presente → "Participacion".
    /// - Aprobacion + Presente + Calificacion &gt;= PuntajeMinimo → "Aprobacion".
    /// - Aprobacion + Presente + cualquier otro caso (calificación &lt; umbral, null, o umbral null por dato
    ///   inconsistente) → "Asistencia". Fallback seguro: nunca emitimos "Aprobacion" sin evidencia.
    /// </summary>
    internal static string CalcularCertificadoEfectivo(Capacitacion capacitacion, Asistente asistente)
    {
        if (capacitacion.TipoCertificacion != TipoCertificacion.Aprobacion)
        {
            return "Participacion";
        }

        if (asistente.Calificacion.HasValue
            && capacitacion.PuntajeMinimo.HasValue
            && asistente.Calificacion.Value >= capacitacion.PuntajeMinimo.Value)
        {
            return "Aprobacion";
        }

        return "Asistencia";
    }

    private static string ExtractFilename(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return string.Empty;
        // El emisor devuelve rutas estilo "/output/<archivo>.pdf" — se usa el último segmento.
        var idx = ruta.LastIndexOfAny(new[] { '/', '\\' });
        return idx >= 0 && idx < ruta.Length - 1 ? ruta[(idx + 1)..] : ruta;
    }
}
