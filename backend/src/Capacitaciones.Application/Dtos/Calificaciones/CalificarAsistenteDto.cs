namespace Capacitaciones.Application.Dtos.Calificaciones;

/// <summary>
/// Body del endpoint de calificación (pública + admin, Fase 11).
/// <c>Calificacion</c> admite valores entre 0 y 10 (step 0.1) o <c>null</c> para limpiar la nota.
/// El rango lo valida <c>CalificarAsistenteUseCase</c>; aquí se deja <see cref="decimal?"/> para que
/// el binding JSON rechace valores no numéricos antes de llegar al use case.
/// </summary>
public class CalificarAsistenteDto
{
    public decimal? Calificacion { get; set; }
}
