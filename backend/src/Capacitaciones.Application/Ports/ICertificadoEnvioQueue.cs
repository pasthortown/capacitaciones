namespace Capacitaciones.Application.Ports;

/// <summary>
/// Cola en proceso para disparar el envío de certificados en segundo plano. El endpoint
/// admin marca a los asistentes elegibles como <c>Pendiente</c> y encola el id de la
/// capacitación; un <c>BackgroundService</c> consume la cola y procesa los pendientes
/// fuera del ciclo de vida del request HTTP (así nginx no lo corta por timeout y el
/// servidor sigue atendiendo otras peticiones).
/// </summary>
public interface ICertificadoEnvioQueue
{
    /// <summary>Encola una capacitación para que el worker procese sus asistentes pendientes.</summary>
    void Encolar(Guid capacitacionId);

    /// <summary>Espera y devuelve el siguiente id encolado. Bloquea hasta que haya uno o se cancele.</summary>
    ValueTask<Guid> DequeueAsync(CancellationToken ct);
}
