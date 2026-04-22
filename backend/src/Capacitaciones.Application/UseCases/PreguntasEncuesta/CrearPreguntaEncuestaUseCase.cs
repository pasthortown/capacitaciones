using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class CrearPreguntaEncuestaUseCase
{
    public const int MaxTextoLength = 500;

    private readonly IPreguntaEncuestaRepository _repo;
    private readonly ITipoActividadRepository _tiposActividad;

    public CrearPreguntaEncuestaUseCase(
        IPreguntaEncuestaRepository repo,
        ITipoActividadRepository tiposActividad)
    {
        _repo = repo;
        _tiposActividad = tiposActividad;
    }

    public async Task<PreguntaEncuestaDto> ExecuteAsync(
        UpsertPreguntaEncuestaDto input,
        CancellationToken ct)
    {
        var texto = (input.Texto ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(texto))
        {
            throw new PreguntaEncuestaServiceException("TEXTO_VACIO", "El texto de la pregunta es obligatorio.");
        }
        if (texto.Length > MaxTextoLength)
        {
            throw new PreguntaEncuestaServiceException(
                "TEXTO_DEMASIADO_LARGO",
                $"El texto de la pregunta no puede exceder {MaxTextoLength} caracteres.");
        }
        if (input.TipoActividadId == Guid.Empty)
        {
            throw new PreguntaEncuestaServiceException(
                "TIPO_ACTIVIDAD_REQUERIDO",
                "Debe seleccionar el tipo de actividad.");
        }

        var tipo = await _tiposActividad.GetByIdAsync(input.TipoActividadId, ct);
        if (tipo is null)
        {
            throw new PreguntaEncuestaServiceException(
                "TIPO_ACTIVIDAD_NO_ENCONTRADO",
                $"No existe el tipo de actividad con Id={input.TipoActividadId}.");
        }

        var entity = new PreguntaEncuesta
        {
            Id = Guid.NewGuid(),
            TipoActividadId = tipo.Id,
            Texto = texto,
            Activo = input.Activo,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = null
        };

        await _repo.AddAsync(entity, ct);

        // Re-hidratamos para traer el nombre del tipo de actividad (navegación).
        var created = await _repo.GetByIdAsync(entity.Id, ct) ?? entity;
        if (created.TipoActividad is null)
        {
            created.TipoActividad = tipo;
        }
        return PreguntaEncuestaMapper.ToDto(created);
    }
}
