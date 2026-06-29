namespace Capacitaciones.Application.Dtos.Colaboradores;

/// <summary>
/// Payload de alta/edición de un colaborador externo. <c>BirthDate</c> llega como string
/// <c>yyyy-MM-dd</c> (o vacío). <c>Activo</c> es opcional: en edición, enviarlo en <c>true</c>
/// reactiva un externo dado de baja (patrón usado también en Responsables).
/// </summary>
public class ColaboradorRequest
{
    public string? Cedula { get; set; }
    public string? Name { get; set; }
    public string? Society { get; set; }
    public string? City { get; set; }
    public string? WorkArea { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Sex { get; set; }
    public string? BirthDate { get; set; }
    public string? Province { get; set; }
    public string? MaritalStatus { get; set; }
    public string? JobPosition { get; set; }
    public string? Email { get; set; }
    public bool? Activo { get; set; }
}
