namespace Capacitaciones.Application.Ports;

/// <summary>
/// Servicio transaccional que toma el próximo número del contador de convenios y devuelve tanto
/// el número crudo como el código formateado <c>GIC-EC-REG-###</c>. Incrementa el contador en la
/// misma transacción para evitar colisiones.
/// </summary>
public interface IConvenioNumeracionService
{
    Task<(int numero, string codigo)> ClaimNextAsync(CancellationToken ct = default);

    /// <summary>Formatea un número de registro como <c>GIC-EC-REG-###</c> (mínimo 3 dígitos).</summary>
    static string Format(int numero) =>
        "GIC-EC-REG-" + numero.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
}
