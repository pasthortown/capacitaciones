using Capacitaciones.Application.Dtos.Colaboradores;

namespace Capacitaciones.Application.Ports;

/// <summary>
/// Cliente del API de ControlTareas (Sistema Gestión Interno) para traer los colaboradores
/// internos de DOS (<c>GET /empleados</c>). Se autentica con un usuario de servicio vía
/// <c>POST /auth/login</c> (JWT). Config-gated: si la URL no está configurada, <see cref="Enabled"/>
/// es false y las consultas devuelven vacío (la pestaña "DOS" sale vacía, sin romper la app).
/// </summary>
public interface IControlTareasColaboradoresClient
{
    bool Enabled { get; }

    /// <summary>Lista los colaboradores de DOS. Reenvía búsqueda e inactivos al API de ControlTareas.</summary>
    Task<IReadOnlyList<EmpleadoDosDto>> ListarAsync(string? buscar, bool incluirInactivos, CancellationToken ct = default);

    /// <summary>True si la cédula ya existe como empleado en ControlTareas (para impedir duplicar un externo).</summary>
    Task<bool> ExisteCedulaAsync(string cedula, CancellationToken ct = default);
}
