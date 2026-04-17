namespace Capacitaciones.Application.Dtos;

/// <summary>
/// DTO de entrada para crear o editar un ítem de catálogo.
/// </summary>
public class UpsertCatalogoDto
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
