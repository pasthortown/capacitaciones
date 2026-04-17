namespace Capacitaciones.Application.Dtos.Capacitaciones;

/// <summary>
/// Referencia compacta a un ítem de catálogo (id + nombre) — usada en los DTOs de
/// listado/detalle de capacitaciones para devolver el nombre junto con el FK.
/// </summary>
public class CatalogoRefDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
