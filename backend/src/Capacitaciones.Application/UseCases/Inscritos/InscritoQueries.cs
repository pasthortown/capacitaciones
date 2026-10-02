using Capacitaciones.Application.Dtos.Inscritos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Inscritos;

/// <summary>Lista admin de inscritos (todas las capacitaciones activas o una), sin firmas.</summary>
public class ListarInscritosUseCase
{
    private readonly IInscritoRepository _inscritos;

    public ListarInscritosUseCase(IInscritoRepository inscritos)
    {
        _inscritos = inscritos;
    }

    public async Task<IReadOnlyList<InscritoDto>> ExecuteAsync(Guid? capacitacionId, string? buscar, CancellationToken ct = default)
    {
        var filtro = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim();
        var items = await _inscritos.ListAsync(capacitacionId, filtro, ct);
        return items.Select(i => new InscritoDto
        {
            Id = i.Id,
            CapacitacionId = i.CapacitacionId,
            CapacitacionCodigo = i.CapacitacionCodigo,
            CapacitacionTema = i.CapacitacionTema,
            Nombres = i.Nombres,
            Apellidos = i.Apellidos,
            Identificacion = i.Identificacion,
            AreaId = i.AreaId,
            AreaNombre = i.AreaNombre,
            Email = i.EmailUsuario,
            FechaInscripcion = i.FechaInscripcion,
            TieneFirma = i.TieneFirma
        }).ToList();
    }
}

/// <summary>Detalle de un inscrito, con la firma (para el modal de ver firma / edición).</summary>
public class ObtenerInscritoUseCase
{
    private readonly IInscritoRepository _inscritos;

    public ObtenerInscritoUseCase(IInscritoRepository inscritos)
    {
        _inscritos = inscritos;
    }

    public async Task<InscritoDetalleDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var asistente = await _inscritos.GetDetalleAsync(id, ct) ?? throw new InscritoNotFoundException();
        return InscritoMapper.ToDetalle(asistente);
    }
}

internal static class InscritoMapper
{
    internal const string EmailDomain = "@dos.com.ec";

    internal static InscritoDetalleDto ToDetalle(Asistente a)
    {
        var email = a.EmailUsuario ?? string.Empty;
        var local = email.EndsWith(EmailDomain, StringComparison.OrdinalIgnoreCase) ? email[..^EmailDomain.Length] : email;
        var firma = string.IsNullOrWhiteSpace(a.Firma) ? null : a.Firma;
        return new InscritoDetalleDto
        {
            Id = a.Id,
            CapacitacionId = a.CapacitacionId,
            CapacitacionCodigo = a.Capacitacion?.Codigo ?? string.Empty,
            CapacitacionTema = a.Capacitacion?.Tema ?? string.Empty,
            Nombres = a.Nombres,
            Apellidos = a.Apellidos,
            Identificacion = a.Identificacion,
            AreaId = a.AreaId,
            AreaNombre = a.Area?.Nombre ?? string.Empty,
            Email = email,
            EmailUsuario = local,
            FechaInscripcion = a.FechaInscripcion,
            TieneFirma = firma is not null,
            Firma = firma
        };
    }
}
