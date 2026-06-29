namespace Capacitaciones.Application.Dtos.Colaboradores;

/// <summary>Resultado de resolver un colaborador por cédula (para asociarlo a un convenio).</summary>
public class ColaboradorLookupDto
{
    public string Cedula { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>"DOS" o "Externo".</summary>
    public string Origen { get; set; } = string.Empty;
}
