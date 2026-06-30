namespace Capacitaciones.Application.Dtos.Convenios;

/// <summary>DTO de lectura/respuesta del contador de numeración de convenios <c>GIC-EC-REG-###</c>.</summary>
public class ConvenioNumeracionDto
{
    public int SiguienteNumero { get; set; }
    public DateTime? UltimaActualizacion { get; set; }

    /// <summary>Formato canónico (documentación al cliente).</summary>
    public string Formato { get; set; } = "GIC-EC-REG-###";

    /// <summary>Vista previa del próximo código a asignar (ej. <c>GIC-EC-REG-048</c>).</summary>
    public string SiguienteCodigo { get; set; } = string.Empty;
}

/// <summary>Payload de <c>PUT /api/convenios/numeracion</c>.</summary>
public class UpdateConvenioNumeracionDto
{
    /// <summary>Próximo número a asignar (≥ 1 y mayor al último ya emitido).</summary>
    public int SiguienteNumero { get; set; }
}
