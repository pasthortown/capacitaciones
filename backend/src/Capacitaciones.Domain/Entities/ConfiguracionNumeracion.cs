namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Fila única (Id = 1) con el próximo número a asignar en el código
/// <c>CAP-PC-REG-###</c> (3 dígitos). Consumida por el servicio de numeración
/// (<c>INumeracionService</c> en la capa Application).
/// </summary>
public class ConfiguracionNumeracion
{
    /// <summary>Siempre vale 1. Se modela como int para facilitar el seed por migración.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Próximo número a asignar. Rango válido: 1..999.</summary>
    public int SiguienteNumero { get; set; } = 1;

    /// <summary>Fecha UTC de la última actualización manual del contador.</summary>
    public DateTime? UltimaActualizacion { get; set; }
}
