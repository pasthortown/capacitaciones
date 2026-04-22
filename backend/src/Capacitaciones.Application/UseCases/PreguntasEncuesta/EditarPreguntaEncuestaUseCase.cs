using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class EditarPreguntaEncuestaUseCase
{
    public const int MaxTextoLength = 500;

    private readonly IPreguntaEncuestaRepository _repo;
    private readonly ITipoActividadRepository _tiposActividad;

    public EditarPreguntaEncuestaUseCase(
        IPreguntaEncuestaRepository repo,
        ITipoActividadRepository tiposActividad)
    {
        _repo = repo;
        _tiposActividad = tiposActividad;
    }

    public async Task<PreguntaEncuestaDto> ExecuteAsync(
        Guid id,
        UpsertPreguntaEncuestaDto input,
        CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct)
            ?? throw new PreguntaEncuestaNotFoundException(id);

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

        if (input.TipoActividadId != entity.TipoActividadId)
        {
            var tipo = await _tiposActividad.GetByIdAsync(input.TipoActividadId, ct);
            if (tipo is null)
            {
                throw new PreguntaEncuestaServiceException(
                    "TIPO_ACTIVIDAD_NO_ENCONTRADO",
                    $"No existe el tipo de actividad con Id={input.TipoActividadId}.");
            }
            entity.TipoActividadId = tipo.Id;
            entity.TipoActividad = tipo;
        }

        entity.Texto = texto;
        entity.Activo = input.Activo;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);

        var updated = await _repo.GetByIdAsync(entity.Id, ct) ?? entity;
        return PreguntaEncuestaMapper.ToDto(updated);
    }
}
