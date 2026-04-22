namespace Capacitaciones.Application.Dtos.Capacitador;

/// <summary>
/// Payload que el capacitador envía a <c>PUT /api/capacitador/capacitacion</c>.
///
/// Semántica de actualización parcial (decisión Fase 4):
///   - Todos los campos son <c>string?</c> y SIEMPRE se aplican como "replace" sobre la entidad.
///   - Si el cliente envía <c>null</c>, el valor se borra (queda <c>null</c> en BD).
///   - Si el cliente NO envía la propiedad en el JSON, la deserialización la deja en <c>null</c>
///     y el backend no puede distinguirlo de un borrado explícito; por lo tanto el Frontend
///     debe preservar los valores existentes reenviándolos (GET → editar localmente → PUT con
///     los 4 campos ya resueltos). Se optó por esta convención por simplicidad; evita la
///     complejidad de JsonPatchDocument o flags "Set*" por campo.
///
/// Los strings vienen con <c>Trim()</c> aplicado por el use case; se guarda <c>null</c> si el
/// contenido queda vacío después del trim (normaliza "" ≡ null en persistencia).
/// </summary>
public class UpdateCapacitadorCapacitacionDto
{
    /// <summary>
    /// Nombre del capacitador — obligatorio. El capacitador puede corregir errores que el
    /// admin haya cometido al crearlo. Si se envía <c>null</c> o cadena vacía el caso de uso
    /// rechaza la operación.
    /// </summary>
    public string? Capacitador { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Base64 de la firma (data URL aceptada). Se guarda tal cual.</summary>
    public string? FirmaCapacitador { get; set; }

    public string? CargoCapacitador { get; set; }

    public string? EmpresaCapacitador { get; set; }

    public string? EmailCapacitador { get; set; }
}
