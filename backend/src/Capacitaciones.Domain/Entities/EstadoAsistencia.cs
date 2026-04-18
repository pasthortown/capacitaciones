namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Estado de asistencia marcado por el capacitador en el pase de lista (Fase 10).
/// Se persiste como int nullable: <c>null</c> significa "sin registrar".
/// </summary>
public enum EstadoAsistencia
{
    Presente = 1,
    Ausente = 2
}
