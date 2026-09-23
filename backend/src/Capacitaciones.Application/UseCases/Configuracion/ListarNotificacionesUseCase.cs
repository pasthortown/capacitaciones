using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Caso de uso: listar los tipos de notificación con su regla y variables disponibles.</summary>
public class ListarNotificacionesUseCase
{
    private readonly IConfiguracionNotificacionRepository _repo;

    public ListarNotificacionesUseCase(IConfiguracionNotificacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<NotificacionDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var filas = await _repo.ListAsync(ct);
        return ToDtos(filas);
    }

    /// <summary>Ordena según el catálogo; plantillas en BD que no están en el catálogo se omiten.</summary>
    internal static List<NotificacionDto> ToDtos(IEnumerable<ConfiguracionNotificacion> filas)
    {
        var porPlantilla = filas.ToDictionary(f => f.Plantilla, StringComparer.Ordinal);
        return NotificacionesCatalogo.Todas
            .Where(d => porPlantilla.ContainsKey(d.Plantilla))
            .Select(d =>
            {
                var f = porPlantilla[d.Plantilla];
                return new NotificacionDto
                {
                    Plantilla = d.Plantilla,
                    Nombre = d.Nombre,
                    Activo = f.Activo,
                    AsuntoPersonalizado = f.AsuntoPersonalizado,
                    AsuntoActual = d.AsuntoActual,
                    Variables = d.Variables.Append(NotificacionesCatalogo.VariableAsuntoOriginal).ToList()
                };
            })
            .ToList();
    }
}
