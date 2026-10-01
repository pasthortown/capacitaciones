using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: activar/desactivar notificaciones y fijar asuntos personalizados.</summary>
public class ActualizarNotificacionesUseCase
{
    public const int MaxAsunto = 500;

    private readonly IConfiguracionNotificacionRepository _repo;

    public ActualizarNotificacionesUseCase(IConfiguracionNotificacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<NotificacionDto>> ExecuteAsync(
        List<UpdateNotificacionDto> input, string adminEmail, CancellationToken ct = default)
    {
        if (input is null) throw new ConfiguracionCorreoException("INVALID_INPUT", "Payload requerido.");

        // Validar todo antes de tocar entidades: o se aplica el lote completo o nada.
        foreach (var item in input)
        {
            if (NotificacionesCatalogo.Buscar(item.Plantilla) is null)
            {
                throw new ConfiguracionCorreoException(
                    "PLANTILLA_DESCONOCIDA", $"La plantilla '{item.Plantilla}' no existe.");
            }
            if ((item.AsuntoPersonalizado?.Trim().Length ?? 0) > MaxAsunto)
            {
                throw new ConfiguracionCorreoException(
                    "ASUNTO_MUY_LARGO", $"El asunto de '{item.Plantilla}' supera {MaxAsunto} caracteres.");
            }
        }

        var filas = await _repo.ListAsync(ct);
        var ahora = DateTime.UtcNow;
        foreach (var item in input)
        {
            var fila = filas.FirstOrDefault(f => f.Plantilla == item.Plantilla);
            if (fila is null) continue;

            fila.Activo = item.Activo;
            fila.AsuntoPersonalizado = string.IsNullOrWhiteSpace(item.AsuntoPersonalizado)
                ? null
                : item.AsuntoPersonalizado.Trim();
            fila.ActualizadoPor = adminEmail;
            fila.ActualizadoEn = ahora;
        }

        await _repo.SaveChangesAsync(ct);
        return ListarNotificacionesUseCase.ToDtos(filas);
    }
}
