using Capacitaciones.Application.Dtos.Encuesta;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.PreguntasEncuesta;

public class CrearPreguntaEncuestaUseCase
{
    public const int MaxTextoLength = 500;
    public const int MaxOpcionLength = 200;

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
            Texto = input.Texto.Trim(),
            TipoPregunta = tipoPregunta,
            OpcionesJson = PreguntaEncuestaMapper.SerializeOpciones(opcionesNormalizadas),
            Activo = input.Activo,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = null
        };

        await _repo.AddAsync(entity, ct);

        var created = await _repo.GetByIdAsync(entity.Id, ct) ?? entity;
        if (created.TipoActividad is null)
        {
            created.TipoActividad = tipo;
        }
        return PreguntaEncuestaMapper.ToDto(created);
    }
}
