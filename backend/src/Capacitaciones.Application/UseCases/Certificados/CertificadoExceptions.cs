using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Certificados;

/// <summary>
/// Se lanza cuando la capacitación no está <c>Finalizada</c> o cualquier otra precondición
/// impide emitir/descargar el certificado. El código <c>CAPACITACION_NO_FINALIZADA</c> se
/// traduce a <c>409 Conflict</c>.
/// </summary>
public class CertificadoNoDisponibleException : CapacitacionServiceException
{
    public CertificadoNoDisponibleException(string codigo, string mensaje)
        : base(codigo, mensaje)
    {
    }

    public static CertificadoNoDisponibleException CapacitacionNoFinalizada() =>
        new("CAPACITACION_NO_FINALIZADA",
            "El certificado solo puede emitirse cuando la capacitación esté finalizada.");

    public static CertificadoNoDisponibleException CapacitacionNoEmiteCertificado() =>
        new("CAPACITACION_NO_EMITE_CERTIFICADO",
            "Esta capacitación está configurada para no emitir certificados.");
}

/// <summary>
/// Se lanza cuando hay firmantes obligatorios sin firma (capacitador y/o responsables).
/// <see cref="Faltantes"/> contiene los nombres para que el UI pueda mostrarlos.
/// El código <c>FIRMAS_FALTANTES</c> se traduce a <c>409 Conflict</c>.
/// </summary>
public class CertificadoFirmasFaltantesException : CapacitacionServiceException
{
    public IReadOnlyList<string> Faltantes { get; }

    public CertificadoFirmasFaltantesException(IReadOnlyList<string> faltantes)
        : base("FIRMAS_FALTANTES",
              $"Faltan firmas de: {string.Join(", ", faltantes)}.")
    {
        Faltantes = faltantes;
    }
}

/// <summary>
/// Fase 12 — se lanza cuando el asistente no es elegible para recibir certificado:
/// está marcado como <c>Ausente</c> o no fue marcado por el capacitador (<c>null</c>).
/// Regla universal (decisión 10): sin importar el tipo de certificación, un ausente
/// nunca recibe certificado. El código <c>ASISTENTE_NO_ELEGIBLE_CERTIFICADO</c> se
/// traduce a <c>409 Conflict</c>.
/// </summary>
public class CertificadoAsistenteNoElegibleException : CapacitacionServiceException
{
    /// <summary>"AUSENTE" | "SIN_MARCAR" — facilita el manejo diferenciado en UI.</summary>
    public string Motivo { get; }

    private CertificadoAsistenteNoElegibleException(string motivo, string mensaje)
        : base("ASISTENTE_NO_ELEGIBLE_CERTIFICADO", mensaje)
    {
        Motivo = motivo;
    }

    public static CertificadoAsistenteNoElegibleException Ausente() =>
        new("AUSENTE", "El asistente está marcado como ausente y no recibe certificado.");

    public static CertificadoAsistenteNoElegibleException SinMarcar() =>
        new("SIN_MARCAR", "El asistente no fue marcado en el pase de lista y no recibe certificado.");
}
