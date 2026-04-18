namespace Capacitaciones.Application.Dtos.PaseLista;

/// <summary>
/// Body del endpoint de marcación de asistencia (pública + admin, Fase 10).
/// <c>EstadoAsistencia</c> admite <c>"Presente"</c>, <c>"Ausente"</c> o <c>null</c> para limpiar la marcación.
/// La validación y el parseo al enum <c>EstadoAsistencia</c> viven en
/// <c>MarcarAsistenciaUseCase</c>/el controller — aquí se recibe como string para no acoplar el
/// contrato HTTP al enum numérico.
/// </summary>
public class MarcarAsistenciaDto
{
    public string? EstadoAsistencia { get; set; }
}
