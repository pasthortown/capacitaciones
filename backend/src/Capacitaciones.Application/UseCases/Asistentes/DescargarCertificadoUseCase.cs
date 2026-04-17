using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Asistentes;

/// <summary>
/// Stub Fase 5 del endpoint admin <c>GET /api/capacitaciones/{capacitacionId}/asistentes/{asistenteId}/certificado</c>.
///
/// Política:
///   - Si la capacitación no está <c>Finalizada</c> → lanza <see cref="CapacitacionServiceException"/>
///     código <c>CAPACITACION_NO_FINALIZADA</c> (409 Conflict).
///   - Si está Finalizada → lanza <see cref="CertificadoNoDisponibleException"/> (501 Not Implemented).
///     La integración real con <c>emisor_documentos</c> (Node + Puppeteer) se hará en Fase 6.
///
/// IMPORTANTE: este use case NO llama a ningún servicio externo. Solo valida estado y devuelve la
/// excepción correspondiente. El controller la traduce al HTTP apropiado.
/// </summary>
public class DescargarCertificadoUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public DescargarCertificadoUseCase(ICapacitacionRepository capacitaciones, IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task ExecuteAsync(Guid capacitacionId, Guid asistenteId, CancellationToken ct = default)
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
            throw new CapacitacionServiceException(
                "CAPACITACION_NO_FINALIZADA",
                "El certificado solo puede descargarse cuando la capacitación esté finalizada.");
        }

        throw new CertificadoNoDisponibleException();
    }
}

/// <summary>
/// Se lanza cuando la capacitación está <c>Finalizada</c> pero la integración con el emisor
/// de certificados aún no está disponible (Fase 6). El controller la traduce a 501 Not Implemented.
/// </summary>
public class CertificadoNoDisponibleException : CapacitacionServiceException
{
    public CertificadoNoDisponibleException()
        : base("CERTIFICADO_NO_DISPONIBLE", "Pendiente integración con emisor_documentos (Fase 6).")
    {
    }
}
