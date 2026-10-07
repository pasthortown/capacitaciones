using Capacitaciones.Application.Dtos.Inscritos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Inscritos;

/// <summary>
/// Alta admin de un inscrito (pantalla "Inscritos" → "Agregar inscrito"), para colaboradores que
/// asistieron pero no se inscribieron por el link. A diferencia de la inscripción pública, se permite
/// aunque la capacitación ya esté <c>Finalizada</c> (sí debe existir y estar activa).
///
/// Mismas reglas de datos que la inscripción pública (trim, requeridos, correo sin '@', área activa,
/// cédula única por capacitación), salvo la firma, que es opcional: la registrada si se pide, la nueva
/// si llega, o ninguna. <c>Asistio</c> deja al inscrito <c>Presente</c> (elegible a certificado) o sin
/// marcar.
///
/// Crea o actualiza la <see cref="PersonaInscrita"/> en el mismo SaveChanges; nunca le borra la firma
/// guardada por agregar sin firma.
/// </summary>
public class AgregarInscritoUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAreaRepository _areas;
    private readonly IAsistenteRepository _asistentes;
    private readonly IPersonaInscritaRepository _personas;

    public AgregarInscritoUseCase(
        ICapacitacionRepository capacitaciones,
        IAreaRepository areas,
        IAsistenteRepository asistentes,
        IPersonaInscritaRepository personas)
    {
        _capacitaciones = capacitaciones;
        _areas = areas;
        _asistentes = asistentes;
        _personas = personas;
    }

    public async Task<InscritoDto> ExecuteAsync(AgregarInscritoDto input, CancellationToken ct = default)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(input.CapacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(input.CapacitacionId);

        if (!capacitacion.Activo)
        {
            throw new CapacitacionServiceException("CAPACITACION_INACTIVA", "La capacitación está inactiva.");
        }

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

        var area = await _areas.GetByIdAsync(input.AreaId, ct);
        if (area is null || !area.Activo)
        {
            throw new InscripcionAreaInvalidaException();
        }

        if (await _asistentes.ExistsByCapacitacionAndIdentificacionAsync(capacitacion.Id, identificacion, ct))
        {
            throw new InscripcionDuplicadaException();
        }

        var persona = await _personas.GetByIdentificacionAsync(identificacion, ct);
        string? firmaNueva = null;
        string? firma;
        if (input.UsarFirmaRegistrada)
        {
            if (persona is null || string.IsNullOrWhiteSpace(persona.Firma))
            {
                throw new FirmaRegistradaNoDisponibleException();
            }
            firma = persona.Firma;
        }
        else
        {
            firmaNueva = string.IsNullOrWhiteSpace(input.Firma) ? null : input.Firma.Trim();
            firma = firmaNueva;
        }

        var ahora = DateTime.UtcNow;
        var entity = new Asistente
        {
            Id = Guid.NewGuid(),
            CapacitacionId = capacitacion.Id,
            Nombres = nombres,
            Apellidos = apellidos,
            Identificacion = identificacion,
            AreaId = area.Id,
            EmailUsuario = emailUsuario + InscritoMapper.EmailDomain,
            Firma = firma ?? string.Empty,
            FechaInscripcion = ahora,
            EstadoAsistencia = input.Asistio ? EstadoAsistencia.Presente : null,
            FechaMarcacionAsistencia = input.Asistio ? ahora : null
        };

        if (persona is null)
        {
            _personas.Agregar(new PersonaInscrita
            {
                Id = Guid.NewGuid(),
                Identificacion = identificacion,
                Nombres = nombres,
                Apellidos = apellidos,
                AreaId = area.Id,
                EmailUsuario = entity.EmailUsuario,
                Firma = firma,
                FechaCreacion = ahora,
                FechaActualizacion = ahora
            });
        }
        else
        {
            persona.Nombres = nombres;
            persona.Apellidos = apellidos;
            persona.AreaId = area.Id;
            persona.EmailUsuario = entity.EmailUsuario;
            persona.FechaActualizacion = ahora;
            if (firmaNueva is not null)
            {
                persona.Firma = firmaNueva;
            }
        }

        // Mismo repositorio que la inscripción pública: traduce la violación del índice único a
        // InscripcionDuplicadaException / InscripcionConcurrenteException.
        await _asistentes.AddAsync(entity, ct);

        return new InscritoDto
        {
            Id = entity.Id,
            CapacitacionId = capacitacion.Id,
            CapacitacionCodigo = capacitacion.Codigo,
            CapacitacionTema = capacitacion.Tema,
            Nombres = entity.Nombres,
            Apellidos = entity.Apellidos,
            Identificacion = entity.Identificacion,
            AreaId = area.Id,
            AreaNombre = area.Nombre,
            Email = entity.EmailUsuario,
            FechaInscripcion = entity.FechaInscripcion,
            TieneFirma = firma is not null
        };
    }

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
