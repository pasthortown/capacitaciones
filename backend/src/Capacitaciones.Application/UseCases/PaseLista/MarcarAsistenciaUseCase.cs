using Capacitaciones.Application.Dtos.PaseLista;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.PaseLista;

/// <summary>
/// Fase 10: marca (o limpia) la asistencia de un asistente.
/// Compartido entre el endpoint público con token PaseLista y el endpoint admin.
/// El caller valida el policy HTTP; el use case solo valida que el asistente pertenezca
/// a la capacitación recibida (previene que un token de capacitación X toque un asistente de Y).
/// </summary>
public class MarcarAsistenciaUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public MarcarAsistenciaUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task<MarcacionAsistenciaResponseDto> ExecuteAsync(
        Guid capacitacionId,
        Guid asistenteId,
        EstadoAsistencia? nuevoEstado,
        CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!capacitacion.Activo)
        {
            throw new CapacitadorForbiddenException("La capacitación está inactiva.");
        }

        var asistente = await _asistentes.GetByIdAsync(asistenteId, ct)
            ?? throw new AsistenteNotFoundException(asistenteId);

        // Defensa en profundidad: un token de pase de lista trae la capacitación en su claim.
        // Si el asistente pertenece a otra capacitación, tratarlo como 404 (no revelar que existe
        // en otra capacitación) — el controller público ya aseguró la autenticación.
        if (asistente.CapacitacionId != capacitacion.Id)
        {
            throw new AsistenteNotFoundException(asistenteId);
        }

        asistente.EstadoAsistencia = nuevoEstado;
        // Si el caller limpia la marcación (null) también se limpia la fecha; si marca Presente/Ausente
        // se registra el timestamp UTC actual — suficiente para auditoría ligera. Si en el futuro se
        // necesita histórico completo, habrá que modelar una tabla aparte.
        asistente.FechaMarcacionAsistencia = nuevoEstado is null ? null : DateTime.UtcNow;

        await _asistentes.UpdateAsync(asistente, ct);

        return new MarcacionAsistenciaResponseDto
        {
            Id = asistente.Id,
            EstadoAsistencia = asistente.EstadoAsistencia?.ToString(),
            FechaMarcacionAsistencia = asistente.FechaMarcacionAsistencia
        };
    }

    /// <summary>
    /// Intenta parsear el string del body (case-insensitive) al enum <see cref="EstadoAsistencia"/>.
    /// Acepta <c>null</c>/whitespace como intención de limpiar la marcación. Lanza
    /// <see cref="CapacitacionServiceException"/> con código <c>ESTADO_ASISTENCIA_INVALIDO</c>
    /// si el valor no es reconocido — el controller lo traduce a 400.
    ///
    /// Solo acepta los literales "Presente" / "Ausente" (case-insensitive). Valores numéricos
    /// como "1"/"2" se rechazan a propósito para que el contrato HTTP quede explícito y no se
    /// cuele un mapeo accidental al cambiar el enum en el futuro.
    /// </summary>
    public static EstadoAsistencia? ParseEstado(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();
        if (string.Equals(trimmed, nameof(EstadoAsistencia.Presente), StringComparison.OrdinalIgnoreCase))
        {
            return EstadoAsistencia.Presente;
        }
        if (string.Equals(trimmed, nameof(EstadoAsistencia.Ausente), StringComparison.OrdinalIgnoreCase))
        {
            return EstadoAsistencia.Ausente;
        }

        throw new CapacitacionServiceException(
            "ESTADO_ASISTENCIA_INVALIDO",
            $"Valor de estadoAsistencia inválido: '{raw}'. Valores permitidos: Presente, Ausente, null.");
    }
}

/// <summary>
/// Asistente inexistente o que pertenece a otra capacitación. El controller la traduce a 404.
/// </summary>
public class AsistenteNotFoundException : CapacitacionServiceException
{
    public AsistenteNotFoundException(Guid id)
        : base("ASISTENTE_NOT_FOUND", $"No existe un asistente con Id={id} en la capacitación indicada.")
    {
    }
}
