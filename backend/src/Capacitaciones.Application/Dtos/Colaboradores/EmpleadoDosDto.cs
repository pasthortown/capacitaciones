namespace Capacitaciones.Application.Dtos.Colaboradores;

/// <summary>
/// Colaborador interno de DOS tal como lo devuelve el API de ControlTareas
/// (<c>GET /empleados</c>). Solo lectura en RegistroCapacitaciones. Los nombres de propiedad
/// coinciden 1-a-1 (camelCase) con el JSON de ControlTareas para deserializar sin mapeo manual.
/// </summary>
public class EmpleadoDosDto
{
    public int Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Society { get; set; }
    public string? JobPosition { get; set; }
    public string? WorkArea { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Sex { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Province { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public int FamilyCount { get; set; }
    public bool DataComplete { get; set; }
}
