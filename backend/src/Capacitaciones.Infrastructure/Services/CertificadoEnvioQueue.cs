using System.Threading.Channels;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="ICertificadoEnvioQueue"/> sobre un <see cref="Channel{T}"/>
/// ilimitado. Se registra como singleton: el productor (controller) escribe y el
/// <c>BackgroundService</c> consumidor lee. Idempotente respecto a duplicados: encolar el
/// mismo id dos veces solo provoca una pasada extra del worker, que es no-op si no quedan
/// pendientes.
/// </summary>
public class CertificadoEnvioQueue : ICertificadoEnvioQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void Encolar(Guid capacitacionId) => _channel.Writer.TryWrite(capacitacionId);

    public ValueTask<Guid> DequeueAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}
