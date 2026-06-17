using System.Collections.Concurrent;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Certificados;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Api.BackgroundServices;

/// <summary>
/// Worker en segundo plano que envía los certificados de forma desacoplada del request HTTP.
///
/// Consume <see cref="ICertificadoEnvioQueue"/> y, por cada capacitación encolada, drena a sus
/// asistentes en estado <see cref="EstadoEnvioCertificado.Pendiente"/> generando el PDF y
/// enviando el correo (con reintentos dentro de <see cref="GenerarYEnviarCertificadosUseCase"/>).
///
/// Diseño:
///  - Cada capacitación se procesa en su propia <see cref="Task"/> para no bloquear el bucle de
///    consumo ni limitar al servidor (varias capacitaciones pueden ir en paralelo).
///  - Un <see cref="SemaphoreSlim"/> por capacitación serializa el procesamiento de la MISMA
///    capacitación, evitando dobles envíos si llega a encolarse dos veces.
///  - Cada asistente se procesa en su propio <c>IServiceScope</c> (DbContext aislado).
///  - Al arrancar, re-encola las capacitaciones que tengan pendientes (retoma trabajos
///    interrumpidos por un reinicio del servidor).
///  - Usa el <c>stoppingToken</c> del host, NO el token del request: nginx ya no puede cortarlo.
/// </summary>
public class CertificadoEnvioBackgroundService : BackgroundService
{
    private readonly ICertificadoEnvioQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CertificadoEnvioBackgroundService> _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public CertificadoEnvioBackgroundService(
        ICertificadoEnvioQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<CertificadoEnvioBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RetomarPendientesAlArrancarAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid capacitacionId;
            try
            {
                capacitacionId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // No await: procesamos en paralelo por capacitación. La serialización por id la
            // garantiza el semáforo dentro de ProcesarCapacitacionAsync.
            _ = Task.Run(() => ProcesarCapacitacionAsync(capacitacionId, stoppingToken), CancellationToken.None);
        }
    }

    private async Task RetomarPendientesAlArrancarAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var asistentes = scope.ServiceProvider.GetRequiredService<IAsistenteRepository>();
            var ids = await asistentes.ListCapacitacionesConPendientesAsync(ct);
            foreach (var id in ids)
            {
                _queue.Encolar(id);
            }
            if (ids.Count > 0)
            {
                _logger.LogInformation(
                    "Retomando envío de certificados: {Count} capacitación(es) con pendientes.", ids.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudieron retomar los envíos pendientes al arrancar.");
        }
    }

    private async Task ProcesarCapacitacionAsync(Guid capacitacionId, CancellationToken stoppingToken)
    {
        var gate = _locks.GetOrAdd(capacitacionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(stoppingToken);
        try
        {
            // Drenamos en pasadas: tras procesar los pendientes actuales, re-consultamos por si
            // un "reintentar erróneos" agregó nuevos durante la corrida. Termina cuando no quedan.
            while (!stoppingToken.IsCancellationRequested)
            {
                List<Guid> pendientes;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var asistentes = scope.ServiceProvider.GetRequiredService<IAsistenteRepository>();
                    var lista = await asistentes.ListByEstadoEnvioAsync(
                        capacitacionId, EstadoEnvioCertificado.Pendiente, stoppingToken);
                    pendientes = lista.Select(a => a.Id).ToList();
                }

                if (pendientes.Count == 0) break;

                _logger.LogInformation(
                    "Enviando {Count} certificado(s) de la capacitación {Id}.", pendientes.Count, capacitacionId);

                foreach (var asistenteId in pendientes)
                {
                    if (stoppingToken.IsCancellationRequested) return;

                    using var scope = _scopeFactory.CreateScope();
                    var useCase = scope.ServiceProvider.GetRequiredService<GenerarYEnviarCertificadosUseCase>();
                    try
                    {
                        await useCase.ProcesarUnoAsync(capacitacionId, asistenteId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        // ProcesarUnoAsync ya persiste sus propios errores; esto es una red de
                        // seguridad para que un fallo inesperado no tumbe el worker.
                        _logger.LogError(ex,
                            "Fallo inesperado enviando certificado del asistente {AsistenteId} (capacitación {Id}).",
                            asistenteId, capacitacionId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado del host: salimos limpiamente. Los pendientes se retoman al reiniciar.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando el envío de certificados de la capacitación {Id}.", capacitacionId);
        }
        finally
        {
            gate.Release();
        }
    }
}
