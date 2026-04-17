using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Capacitaciones;

/// <summary>
/// Calcula el estado derivado de una capacitación a partir del reloj actual (UTC).
/// </summary>
public static class CapacitacionEstadoCalculator
{
    public const string InscripcionesAbiertas = "Inscripciones Abiertas";
    public const string Iniciada = "Iniciada";
    public const string Finalizada = "Finalizada";

    public static string Calcular(Capacitacion c) => Calcular(c.FechaHoraInicio, c.DuracionMinutos, DateTime.UtcNow);

    public static string Calcular(DateTime fechaInicio, int duracionMinutos, DateTime ahoraUtc)
    {
        // Se asume que FechaHoraInicio se guarda en UTC (o al menos en la misma referencia que ahoraUtc).
        if (ahoraUtc < fechaInicio) return InscripcionesAbiertas;

        var fin = fechaInicio.AddMinutes(duracionMinutos);
        if (ahoraUtc < fin) return Iniciada;

        return Finalizada;
    }
}
