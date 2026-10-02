using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Inscritos;

/// <summary>El inscrito (asistente) no existe o pertenece a una capacitación eliminada. El controlador responde 404.</summary>
public class InscritoNotFoundException : CapacitacionServiceException
{
    public InscritoNotFoundException()
        : base("INSCRITO_NO_ENCONTRADO", "El inscrito no existe.")
    {
    }
}
