using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Asistentes;

/// <summary>
/// Reporte PDF de asistencia (modelo "Registro de Capacitación de Personal"). A diferencia
/// de los certificados, este es por capacitación (no por asistente) y siempre se regenera
/// al momento — no cachea. El emisor lo deposita en el volumen compartido y luego
/// aquí se abre el stream para retornarlo al controller como <c>FileStreamResult</c>.
///
/// Reglas clave:
///   - Lista todos los inscritos, ordenados alfabéticamente por Apellidos → Nombres
///     (OrdinalIgnoreCase, mismo criterio que pase de lista/calificaciones).
///   - La firma del asistente SOLO se envía al emisor si <c>EstadoAsistencia == Presente</c>.
///     Ausentes y no-marcados viajan con <c>FirmaBase64 = null</c>; el emisor los pinta
///     como "Ausente" / celda en blanco respectivamente.
///   - Si la capacitación no existe → 404. No exige estar Finalizada (el registro impreso
///     puede necesitarse antes del cierre).
/// </summary>
public class DescargarReporteAsistenciaUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly IEmisorDocumentosClient _emisor;
    private readonly CertificadosOptions _options;

    public DescargarReporteAsistenciaUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        IEmisorDocumentosClient emisor,
        CertificadosOptions options)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _emisor = emisor;
        _options = options;
    }

    public async Task<CertificadoDescargaDto> ExecuteAsync(Guid capacitacionId, CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var items = await _asistentes.ListByCapacitacionAsync(capacitacion.Id, ct);

        var ordenados = items
            .OrderBy(a => a.Apellidos ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Nombres ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = BuildPayload(capacitacion, ordenados);

        var resultado = await _emisor.EmitirReporteAsistenciaAsync(payload, ct);

        var filename = ExtractFilename(resultado.Ruta);
        var outputDir = string.IsNullOrWhiteSpace(_options.OutputDir) ? "/output" : _options.OutputDir;
        var fullPath = Path.Combine(outputDir, filename);

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"El reporte no fue encontrado en '{fullPath}' tras la emisión. " +
                "Verifica que el volumen '/output' esté montado correctamente en el backend.");
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new CertificadoDescargaDto(stream, filename);
    }

    private static ReporteAsistenciaRequest BuildPayload(
        Capacitacion capacitacion,
        IReadOnlyList<Asistente> ordenados)
    {
        var fechaUtc = DateTime.SpecifyKind(capacitacion.FechaHoraInicio, DateTimeKind.Utc);
        var fechaIso = fechaUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var duracionHoras = capacitacion.DuracionMinutos / 60m;

        return new ReporteAsistenciaRequest
        {
            Capacitacion = new ReporteAsistenciaCapacitacionDto
            {
                Codigo = capacitacion.Codigo,
                Tema = capacitacion.Tema,
                Capacitador = capacitacion.Capacitador,
                FirmaCapacitadorBase64 = capacitacion.FirmaCapacitador,
                FechaInicio = fechaIso,
                DuracionHoras = duracionHoras,
                Departamento = capacitacion.EmpresaCapacitador, // proxy razonable mientras no exista un campo propio
                Descripcion = capacitacion.Descripcion
            },
            Asistentes = ordenados.Select(a => new ReporteAsistenciaAsistenteDto
            {
                Nombres = a.Nombres,
                Apellidos = a.Apellidos,
                Identificacion = a.Identificacion,
                Area = a.Area?.Nombre,
                EstadoAsistencia = a.EstadoAsistencia?.ToString(),
                // Requisito explícito: la firma solo viaja para quien estuvo Presente.
                FirmaBase64 = a.EstadoAsistencia == EstadoAsistencia.Presente ? a.Firma : null
            }).ToList()
        };
    }

    private static string ExtractFilename(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return string.Empty;
        var idx = ruta.LastIndexOfAny(new[] { '/', '\\' });
        return idx >= 0 && idx < ruta.Length - 1 ? ruta[(idx + 1)..] : ruta;
    }
}
