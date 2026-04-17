namespace Capacitaciones.Application.Dtos.Configuracion;

/// <summary>Payload de <c>PUT /api/configuracion/numeracion</c>.</summary>
public class UpdateConfiguracionNumeracionDto
{
    /// <summary>Próximo número a asignar. Rango válido: 1..999.</summary>
    public int SiguienteNumero { get; set; }
}
