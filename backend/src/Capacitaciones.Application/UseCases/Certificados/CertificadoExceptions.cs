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
