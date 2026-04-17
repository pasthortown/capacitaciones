namespace Capacitaciones.Application.Dtos.Configuracion;

/// <summary>
/// DTO de lectura/respuesta para el contador de numeración <c>CAP-PC-REG-###</c>.
/// </summary>
public class ConfiguracionNumeracionDto
{
    public int SiguienteNumero { get; set; }
    public DateTime? UltimaActualizacion { get; set; }

    /// <summary>Formato canónico. Valor fijo para documentar al cliente.</summary>
    public string Formato { get; set; } = "CAP-PC-REG-###";
}
