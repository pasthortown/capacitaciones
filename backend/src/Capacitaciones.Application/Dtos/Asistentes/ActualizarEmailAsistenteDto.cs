namespace Capacitaciones.Application.Dtos.Asistentes;

/// <summary>Body del endpoint admin para corregir el email de un asistente.</summary>
public class ActualizarEmailAsistenteDto
{
    public string? Email { get; set; }
}

/// <summary>Respuesta de la actualización de email de un asistente.</summary>
public class EmailAsistenteResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
}
