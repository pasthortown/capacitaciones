using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Inscripcion;

/// <summary>
/// Se lanza cuando el link firmado es válido pero la capacitación ya está <c>Finalizada</c>
/// y no se aceptan más inscripciones. El controlador la traduce a 409 Conflict.
/// Hereda de <see cref="CapacitacionServiceException"/> para compartir el patrón de
/// código+mensaje usado por el resto de excepciones de dominio.
/// </summary>
public class InscripcionCerradaException : CapacitacionServiceException
{
    public InscripcionCerradaException()
        : base("INSCRIPCION_CERRADA", "La capacitación ya finalizó y no acepta más inscripciones.")
    {
    }
}

/// <summary>
/// Se lanza cuando la (capacitación, identificación) ya está inscrita. El controlador la traduce a 409.
/// </summary>
public class InscripcionDuplicadaException : CapacitacionServiceException
{
    public InscripcionDuplicadaException()
        : base("INSCRIPCION_DUPLICADA", "Ya existe una inscripción con esa identificación para esta capacitación.")
    {
    }
}

/// <summary>
/// Se lanza cuando el <c>AreaId</c> informado no existe o está inactivo.
/// </summary>
public class InscripcionAreaInvalidaException : CapacitacionServiceException
{
    public InscripcionAreaInvalidaException()
        : base("AREA_INVALIDA", "El área seleccionada no existe o está inactiva.")
    {
    }
}

/// <summary>
/// Se lanza cuando la inscripción pide usar la firma registrada pero la identificación no tiene
/// persona registrada o no tiene firma guardada. El controlador la traduce a 400.
/// </summary>
public class FirmaRegistradaNoDisponibleException : CapacitacionServiceException
{
    public FirmaRegistradaNoDisponibleException()
        : base("FIRMA_REGISTRADA_NO_DISPONIBLE", "No hay una firma registrada para esta identificación.")
    {
    }
}

/// <summary>
/// Se lanza cuando dos inscripciones simultáneas intentan crear la misma <c>PersonaInscrita</c>
/// (choque con <c>UX_PersonaInscrita_Identificacion</c>). No se guarda nada; el controlador responde
/// 409 y el cliente puede reintentar (la persona ya existirá y se actualizará).
/// </summary>
public class InscripcionConcurrenteException : CapacitacionServiceException
{
    public InscripcionConcurrenteException()
        : base("INSCRIPCION_CONCURRENTE", "Se registró otra inscripción con esta identificación al mismo tiempo. Vuelve a intentarlo.")
    {
    }
}
