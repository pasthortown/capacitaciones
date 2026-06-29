namespace Capacitaciones.Application.Dtos.Colaboradores;

/// <summary>
/// Colaborador <b>externo a DOS</b> (administrado localmente en RegistroCapacitaciones).
/// Comparte la forma de campos con <see cref="EmpleadoDosDto"/> para que la UI muestre ambas
/// pestañas con las mismas columnas.
/// </summary>
public class ColaboradorDto
{
    public Guid Id { get; set; }
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
    public string? MaritalStatus { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
}
