namespace Capacitaciones.Application.Ports;

/// <summary>
/// Servicio transaccional que toma el próximo número del contador de numeración
/// y lo formatea como <c>CAP-PC-REG-###</c> (3 dígitos). Incrementa el contador en la
/// misma transacción para evitar colisiones.
/// </summary>
/// <remarks>
/// No se invoca en Fase 2: se deja listo para Fase 3 (creación de capacitaciones).
/// </remarks>
public interface INumeracionService
{
    Task<string> ClaimNextCodeAsync(CancellationToken ct = default);
}
