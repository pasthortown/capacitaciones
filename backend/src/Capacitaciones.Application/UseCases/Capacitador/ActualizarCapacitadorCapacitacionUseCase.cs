using Capacitaciones.Application.Dtos.Capacitador;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;

namespace Capacitaciones.Application.UseCases.Capacitador;

/// <summary>
/// Caso de uso Fase 4: el capacitador autenticado vía link firmado actualiza los 4
/// campos que le pertenecen sobre una capacitación.
///
/// Reglas:
///   - Capacitación inexistente → <see cref="CapacitacionNotFoundException"/> (404 en el controller).
///   - Capacitación inactiva → <see cref="CapacitadorForbiddenException"/> (403). El capacitador SÍ
///     puede editar aún cuando la capacitación esté Finalizada (permitimos correcciones post-finalización).
///   - Semántica replace en los 4 campos: se asignan tal cual vinieron, con <c>Trim()</c>;
///     si queda vacío tras trim se guarda <c>null</c>. La firma también se trimea pero no
///     se normaliza más (es base64/dataURL, podría tener caracteres significativos).
/// </summary>
public class ActualizarCapacitadorCapacitacionUseCase
{
    private readonly ICapacitacionRepository _repo;

    public ActualizarCapacitadorCapacitacionUseCase(ICapacitacionRepository repo)
    {
        _repo = repo;
    }

    public async Task<CapacitadorCapacitacionDto> ExecuteAsync(
        Guid capacitacionId,
        UpdateCapacitadorCapacitacionDto input,
        CancellationToken ct = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var entity = await _repo.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!entity.Activo)
        {
            throw new CapacitadorForbiddenException("La capacitación está inactiva.");
        }

        // Nombre del capacitador: obligatorio. Se permite editarlo para corregir errores
        // del admin (el capacitador desde su link firmado puede rectificar su propio nombre).
        var capacitadorNombre = input.Capacitador?.Trim();
        if (string.IsNullOrEmpty(capacitadorNombre))
        {
            throw new CapacitadorForbiddenException("El nombre del capacitador es obligatorio.");
        }
        entity.Capacitador = capacitadorNombre;

        // Replace semántica — si el cliente envía un valor, se aplica tal cual (con Trim).
        // Si queda vacío → null. Si envía null explícito → queda null.
        entity.Descripcion = NormalizeEmptyToNull(input.Descripcion);
        entity.CargoCapacitador = NormalizeEmptyToNull(input.CargoCapacitador);
        entity.EmpresaCapacitador = NormalizeEmptyToNull(input.EmpresaCapacitador);
        entity.EmailCapacitador = NormalizeEmptyToNull(input.EmailCapacitador);
        entity.FirmaCapacitador = NormalizeEmptyToNull(input.FirmaCapacitador);
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);

        return ObtenerCapacitacionCapacitadorUseCase.MapTo(entity);
    }

    private static string? NormalizeEmptyToNull(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
