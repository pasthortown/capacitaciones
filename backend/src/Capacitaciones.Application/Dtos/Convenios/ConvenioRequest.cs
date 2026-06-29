namespace Capacitaciones.Application.Dtos.Convenios;

/// <summary>
/// Payload de alta/edición de un convenio. <c>Cedula</c> identifica al colaborador (se resuelve
/// su nombre/origen en el backend). <c>Fecha</c> en <c>yyyy-MM-dd</c>. <c>MesesADevengar</c> ∈
/// {0,12,24,36} (0 = no aplica devengo). <c>Activo</c> opcional: <c>true</c> reactiva.
/// </summary>
public class ConvenioRequest
{
    public string? Cedula { get; set; }
    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    public string? Tipo { get; set; }
    public string? TipoCurso { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }
    public string? SolicitadoPor { get; set; }
    public string? AutorizadoPor { get; set; }
    public string? Fecha { get; set; }
    public int MesesADevengar { get; set; }
    /// <summary>"Vigente" | "Devengado" | "Cobrado" | "Anulado". Default Vigente.</summary>
    public string? Estado { get; set; }
    public List<ConvenioItemRequest>? Items { get; set; }
    public bool? Activo { get; set; }
}
