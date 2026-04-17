namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Tipo de certificación que se emite al finalizar la capacitación.
/// Se persiste como int (se refleja en el texto del certificado).
/// </summary>
public enum TipoCertificacion
{
    Participacion = 1,
    Aprobacion = 2
}
