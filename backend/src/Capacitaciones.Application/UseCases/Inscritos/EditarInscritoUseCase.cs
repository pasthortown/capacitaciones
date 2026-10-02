using Capacitaciones.Application.Dtos.Inscritos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Inscritos;

/// <summary>
/// Edición admin de un inscrito (pantalla "Inscritos"): nombres, apellidos, cédula, área, correo y
/// firma. Mismas reglas que la inscripción pública (trim, requeridos, correo sin '@', área activa,
/// cédula única por capacitación). Firma null/vacía = conservar la actual.
///
/// También actualiza la <see cref="PersonaInscrita"/> de la cédula final para que el autocompletado
/// use los datos corregidos: se crea si no existe; si existe se actualizan sus datos (solo si es la misma
/// persona y esta es su inscripción más reciente), y la firma solo cuando llega una nueva o la persona no
/// tenía. Asistente y persona se guardan en un solo SaveChanges.
/// </summary>
public class EditarInscritoUseCase
{
    private readonly IInscritoRepository _inscritos;
    private readonly IAreaRepository _areas;
    private readonly IPersonaInscritaRepository _personas;

    public EditarInscritoUseCase(IInscritoRepository inscritos, IAreaRepository areas, IPersonaInscritaRepository personas)
    {
        _inscritos = inscritos;
        _areas = areas;
        _personas = personas;
    }

    public async Task<InscritoDetalleDto> ExecuteAsync(Guid id, EditarInscritoDto input, CancellationToken ct = default)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var asistente = await _inscritos.GetForEditAsync(id, ct) ?? throw new InscritoNotFoundException();

        var nombres = RequireTrimmed(input.Nombres, "nombres");
        var apellidos = RequireTrimmed(input.Apellidos, "apellidos");
        var identificacion = RequireTrimmed(input.Identificacion, "identificacion");
        var emailUsuario = RequireTrimmed(input.EmailUsuario, "emailUsuario");
        if (emailUsuario.Contains('@'))
        {
            throw new CapacitacionServiceException(
                "EMAIL_INVALIDO",
                $"emailUsuario debe contener solo la parte local; el dominio '{InscritoMapper.EmailDomain}' lo agrega el servidor.");
        }

        // Un área desactivada después de la inscripción se acepta si no cambia (para poder
        // corregir otros datos sin forzar un cambio de área).
        var area = await _areas.GetByIdAsync(input.AreaId, ct);
        if (area is null || (!area.Activo && area.Id != asistente.AreaId))
        {
            throw new InscripcionAreaInvalidaException();
        }

        if (identificacion != asistente.Identificacion &&
            await _inscritos.ExistsOtroConIdentificacionAsync(asistente.CapacitacionId, identificacion, asistente.Id, ct))
        {
            throw new InscripcionDuplicadaException();
        }

        var firmaNueva = string.IsNullOrWhiteSpace(input.Firma) ? null : input.Firma.Trim();
        var cedulaCambio = identificacion != asistente.Identificacion;

        asistente.Nombres = nombres;
        asistente.Apellidos = apellidos;
        asistente.Identificacion = identificacion;
        asistente.AreaId = area.Id;
        asistente.Area = area;
        asistente.EmailUsuario = emailUsuario + InscritoMapper.EmailDomain;
        if (firmaNueva is not null)
        {
            asistente.Firma = firmaNueva;
        }

        var firmaFinal = string.IsNullOrWhiteSpace(asistente.Firma) ? null : asistente.Firma;
        var ahora = DateTime.UtcNow;
        var persona = await _personas.GetByIdentificacionAsync(identificacion, ct);
        if (persona is null)
        {
            _personas.Agregar(new PersonaInscrita
            {
                Id = Guid.NewGuid(),
                Identificacion = identificacion,
                Nombres = nombres,
                Apellidos = apellidos,
                AreaId = area.Id,
                EmailUsuario = asistente.EmailUsuario,
                Firma = firmaFinal,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            });
        }
        else if (await DebeActualizarPersonaAsync(persona, asistente, cedulaCambio, nombres, apellidos, ct))
        {
            persona.Nombres = nombres;
            persona.Apellidos = apellidos;
            persona.AreaId = area.Id;
            persona.EmailUsuario = asistente.EmailUsuario;
            persona.FechaActualizacion = ahora;
            if (firmaNueva is not null || string.IsNullOrWhiteSpace(persona.Firma))
            {
                persona.Firma = firmaFinal;
            }
        }

        await _inscritos.SaveChangesAsync(ct);
        return InscritoMapper.ToDetalle(asistente);
    }

    /// <summary>
    /// La persona se actualiza solo si (a) el inscrito sigue siendo esa persona — si se cambió la cédula
    /// a una ya registrada con otro nombre, es un dato de otra persona y no se toca — y (b) esta es su
    /// inscripción más reciente, para no devolver el registro a datos viejos al corregir una inscripción
    /// antigua.
    /// </summary>
    private async Task<bool> DebeActualizarPersonaAsync(
        PersonaInscrita persona, Asistente asistente, bool cedulaCambio, string nombres, string apellidos, CancellationToken ct)
    {
        if (cedulaCambio && (Normalizar(persona.Nombres) != Normalizar(nombres) || Normalizar(persona.Apellidos) != Normalizar(apellidos)))
        {
            return false;
        }
        return !await _inscritos.ExistsInscripcionMasRecienteAsync(asistente.Identificacion, asistente.FechaInscripcion, asistente.Id, ct);
    }

    private static string Normalizar(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string RequireTrimmed(string? value, string field)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new CapacitacionServiceException("CAMPO_REQUERIDO", $"El campo '{field}' es requerido.");
        }
        return trimmed;
    }
}
