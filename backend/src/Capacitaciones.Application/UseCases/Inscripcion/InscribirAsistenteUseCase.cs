using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Inscripcion;

/// <summary>
/// Registra un nuevo asistente para la capacitación autenticada por el token (cid del JWT).
///
/// Validaciones:
///   - Capacitación existe, activa y NO finalizada (si lo está → <see cref="InscripcionCerradaException"/>).
///   - <c>Nombres</c>, <c>Apellidos</c>, <c>Identificacion</c>, <c>EmailUsuario</c>, <c>Firma</c>
///     se trimean y no pueden quedar vacíos.
///   - <c>EmailUsuario</c> valida que NO contenga <c>@</c> (es solo la parte local; el dominio
///     <c>@dos.com.ec</c> lo concatena este caso de uso). Aparte del '@' no se aplican otras
///     reglas — el dominio corporativo acepta cualquier cadena no vacía como usuario.
///   - <c>AreaId</c> debe existir y estar activa.
///   - <c>(CapacitacionId, Identificacion)</c> único → duplicado lanza <see cref="InscripcionDuplicadaException"/>.
///
/// Decisión: el match de duplicado es <b>case-sensitive</b> en la aplicación (compara strings tal cual
/// después del trim). La BD con collation CI (SQL Server por defecto) actuará como segunda línea.
/// Normalizar case de la identificación a nivel aplicación podría esconder datos reales (ej. "AB-123"
/// vs "ab-123" podrían ser personas distintas en pasaportes extranjeros); preferimos no tocar.
/// </summary>
public class InscribirAsistenteUseCase
{
    private const string EmailDomain = "@dos.com.ec";

    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAreaRepository _areas;
    private readonly IAsistenteRepository _asistentes;

    public InscribirAsistenteUseCase(
        ICapacitacionRepository capacitaciones,
        IAreaRepository areas,
        IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _areas = areas;
        _asistentes = asistentes;
    }

    public async Task<AsistenteSummaryDto> ExecuteAsync(
        Guid capacitacionId,
        CreateInscripcionDto input,
        CancellationToken ct = default)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        if (!capacitacion.Activo)
        {
            throw new CapacitacionServiceException(
                "CAPACITACION_INACTIVA",
                "La capacitación está inactiva.");
        }

        if (CapacitacionEstadoCalculator.Calcular(capacitacion) == CapacitacionEstadoCalculator.Finalizada)
        {
            throw new InscripcionCerradaException();
        }

        var nombres = RequireTrimmed(input.Nombres, "nombres");
        var apellidos = RequireTrimmed(input.Apellidos, "apellidos");
        var identificacion = RequireTrimmed(input.Identificacion, "identificacion");
        var emailUsuario = RequireTrimmed(input.EmailUsuario, "emailUsuario");
        var firma = RequireTrimmed(input.Firma, "firma");

        if (emailUsuario.Contains('@'))
        {
            throw new CapacitacionServiceException(
                "EMAIL_INVALIDO",
                $"emailUsuario debe contener solo la parte local; el dominio '{EmailDomain}' lo agrega el servidor.");
        }

        // Validación de área: existe y activa.
        var area = await _areas.GetByIdAsync(input.AreaId, ct);
        if (area is null || !area.Activo)
        {
            throw new InscripcionAreaInvalidaException();
        }

        // Duplicado en aplicación (el índice único es la segunda línea).
        if (await _asistentes.ExistsByCapacitacionAndIdentificacionAsync(capacitacionId, identificacion, ct))
        {
            throw new InscripcionDuplicadaException();
        }

        var entity = new Asistente
        {
            Id = Guid.NewGuid(),
            CapacitacionId = capacitacionId,
            Nombres = nombres,
            Apellidos = apellidos,
            Identificacion = identificacion,
            AreaId = area.Id,
            EmailUsuario = emailUsuario + EmailDomain,
            Firma = firma,
            FechaInscripcion = DateTime.UtcNow
        };

        // El repositorio traduce la violación del UNIQUE INDEX (carrera contra pre-check) a
        // InscripcionDuplicadaException para mantener Application desacoplado de EF.
        await _asistentes.AddAsync(entity, ct);

        return new AsistenteSummaryDto
        {
            Id = entity.Id,
            Nombres = entity.Nombres,
            Apellidos = entity.Apellidos,
            Identificacion = entity.Identificacion,
            Email = entity.EmailUsuario,
            Area = new CatalogoRefDto { Id = area.Id, Nombre = area.Nombre },
            FechaInscripcion = entity.FechaInscripcion
        };
    }

    private static string RequireTrimmed(string? value, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new CapacitacionServiceException(
                "CAMPO_REQUERIDO",
                $"El campo '{field}' es requerido.");
        }
        return trimmed;
    }
}
