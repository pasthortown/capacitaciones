namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Colaborador <b>externo a DOS</b>: persona que no pertenece a la organización (no está en
/// el maestro de ControlTareas/Aranda) pero a la que igual se le registra/capacita.
///
/// Los colaboradores internos de DOS NO se guardan aquí: se consultan en vivo al API de
/// ControlTareas (<c>GET /empleados</c>) y son de solo lectura en RegistroCapacitaciones.
/// Por eso, al crear un externo se valida que su cédula no exista ya en ControlTareas.
///
/// Clave natural: <see cref="Cedula"/> (única). Baja lógica vía <see cref="Activo"/>.
/// Campos espejo del empleado de ControlTareas para que la UI muestre ambas pestañas igual.
/// </summary>
public class Colaborador
{
    public Guid Id { get; set; }

    /// <summary>Cédula / identificación. Clave natural, única entre los externos.</summary>
    public string Cedula { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Society { get; set; }
    public string? City { get; set; }
    public string? WorkArea { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Sex { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Province { get; set; }
    public string? MaritalStatus { get; set; }
    public string? JobPosition { get; set; }
    public string? Email { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
