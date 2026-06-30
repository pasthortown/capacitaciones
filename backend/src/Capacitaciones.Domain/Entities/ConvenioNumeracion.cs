namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Fila única (Id = 1) con el próximo número a asignar en el código de convenios
/// <c>GIC-EC-REG-###</c>. Análoga a <see cref="ConfiguracionNumeracion"/> (que numera las
/// capacitaciones con <c>CAP-PC-REG-###</c>), pero con su propio contador independiente.
/// </summary>
public class ConvenioNumeracion
{
    /// <summary>Siempre vale 1. Se modela como int para facilitar el seed por migración.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Próximo número a asignar (≥ 1).</summary>
    public int SiguienteNumero { get; set; } = 1;

    /// <summary>Fecha UTC de la última actualización manual del contador.</summary>
    public DateTime? UltimaActualizacion { get; set; }
}
