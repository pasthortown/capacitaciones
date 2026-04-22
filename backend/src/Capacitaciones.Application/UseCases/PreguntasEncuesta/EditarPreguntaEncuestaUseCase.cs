using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class EditarPreguntaEncuestaUseCase
{
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

        PreguntaEncuestaValidator.ValidarTexto(input.Texto);
        var tipoPregunta = PreguntaEncuestaValidator.ParseTipoPregunta(input.TipoPregunta);
        var opcionesNormalizadas = PreguntaEncuestaValidator.ValidarYNormalizarOpciones(
            tipoPregunta, input.Opciones);

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

        entity.Texto = input.Texto.Trim();
        entity.TipoPregunta = tipoPregunta;
        entity.OpcionesJson = PreguntaEncuestaMapper.SerializeOpciones(opcionesNormalizadas);
        entity.Activo = input.Activo;
        entity.FechaActualizacion = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);

        var updated = await _repo.GetByIdAsync(entity.Id, ct) ?? entity;
        return PreguntaEncuestaMapper.ToDto(updated);
    }
}
