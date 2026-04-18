using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Certificados;

/// <summary>
/// Caso de uso Fase 6 (lote): emite certificados para todos los asistentes de una capacitación.
/// No aborta al primer error — recorre todos y devuelve el resumen, lo que permite al admin
/// regenerar manualmente los que fallaron sin tener que volver a procesar los que ya salieron.
///
/// Precondición rígida: la capacitación debe estar <c>Finalizada</c>; de lo contrario se lanza
/// 409 antes de iterar. Si la capacitación o el capacitador no tienen firma, cada asistente
/// fallará con <c>FIRMAS_FALTANTES</c> (error redundante pero consistente con el unitario).
/// </summary>
public class GenerarCertificadosCapacitacionUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;
    private readonly GenerarCertificadoAsistenteUseCase _generar;

    public GenerarCertificadosCapacitacionUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes,
        GenerarCertificadoAsistenteUseCase generar)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
        _generar = generar;
    }

    public async Task<GeneracionLoteResultadoDto> ExecuteAsync(
        Guid capacitacionId,
        CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        // Corte temprano: si la capacitación no está finalizada, no tiene sentido iterar.
        // Cada llamada individual también lo rechazaría, pero así devolvemos 409 limpio.
        if (CapacitacionEstadoCalculator.Calcular(capacitacion) != CapacitacionEstadoCalculator.Finalizada)
        {
            throw CertificadoNoDisponibleException.CapacitacionNoFinalizada();
        }

        var asistentes = await _asistentes.ListByCapacitacionAsync(capacitacionId, ct);

        var resultado = new GeneracionLoteResultadoDto
        {
            Total = asistentes.Count
        };

        // Procesamos en serie (no en paralelo) por dos razones:
        //   1. El emisor puede renderizar Puppeteer con cierto peso — disparar N en paralelo
        //      puede saturar la instancia.
        //   2. EF Core DbContext no es thread-safe: si alguna recarga lee del contexto scoped,
        //      paralelizar causaría excepciones.
        foreach (var a in asistentes)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _generar.ExecuteAsync(capacitacionId, a.Id, ct);
                resultado.Emitidos++;
            }
            catch (OperationCanceledException)
            {
                // Respetamos la cancelación propagándola.
                throw;
            }
            catch (CapacitacionServiceException ex)
            {
                resultado.Errores.Add(new GeneracionLoteErrorDto
                {
                    AsistenteId = a.Id,
                    Codigo = ex.Codigo,
                    Mensaje = ex.Message
                });
            }
            catch (HttpRequestException ex)
            {
                resultado.Errores.Add(new GeneracionLoteErrorDto
                {
                    AsistenteId = a.Id,
                    Codigo = "SERVICIO_EMISOR_NO_DISPONIBLE",
                    Mensaje = ex.Message
                });
            }
        }

        return resultado;
    }
}
