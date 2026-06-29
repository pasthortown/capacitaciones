using System.Globalization;
using Capacitaciones.Application.Common;
using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Mapeo y cálculo de devengo del módulo Convenios (según <see cref="EstadoConvenio"/>).</summary>
public static class ConvenioMapper
{
    public static readonly int[] MesesPermitidos = { 0, 12, 24, 36 };

    public static DateTime Hoy() => EcuadorTime.FromUtc(DateTime.UtcNow).Date;

    public static ConvenioDto ToDto(Convenio c)
    {
        var dto = new ConvenioDto
        {
            Id = c.Id,
            CedulaColaborador = c.CedulaColaborador,
            NombreColaborador = c.NombreColaborador,
            OrigenColaborador = c.OrigenColaborador,
            CargoColaborador = c.CargoColaborador,
            AreaColaborador = c.AreaColaborador,
            Titulo = c.Titulo,
            Descripcion = c.Descripcion,
            Tipo = c.Tipo,
            TipoCurso = c.TipoCurso,
            NombreCurso = c.NombreCurso,
            Marca = c.Marca,
            SolicitadoPor = c.SolicitadoPor,
            AutorizadoPor = c.AutorizadoPor,
            Fecha = c.Fecha,
            MesesADevengar = c.MesesADevengar,
            Estado = c.Estado.ToString(),
            FechaCorte = c.FechaCorte,
            Activo = c.Activo,
            FechaCreacion = c.FechaCreacion,
            FechaActualizacion = c.FechaActualizacion,
            Items = c.Items.Select(i => new ConvenioItemDto
            {
                Id = i.Id, Tipo = i.Tipo, Valor = i.Valor, Devengable = i.Devengable, Observacion = i.Observacion,
            }).ToList(),
            Anexos = c.Anexos.OrderBy(a => a.FechaCreacion).Select(a => new ConvenioAnexoDto
            {
                Id = a.Id, NombreOriginal = a.NombreOriginal, TamanoBytes = a.TamanoBytes,
                ContentType = a.ContentType, FechaCreacion = a.FechaCreacion,
            }).ToList(),
        };

        var baseDevengable = c.Items.Where(i => i.Devengable).Sum(i => i.Valor);
        dto.MontoTotal = c.Items.Sum(i => i.Valor);
        dto.MontoDevengable = baseDevengable;
        dto.AplicaDevengo = c.MesesADevengar > 0;
        dto.FechaDevengable100 = c.MesesADevengar > 0 ? c.Fecha.Date.AddMonths(c.MesesADevengar) : null;

        switch (c.Estado)
        {
            case EstadoConvenio.Devengado:
                // Cumplió el plazo sin cobro: devengado 100%, no se traslada costo.
                dto.MesesTranscurridos = c.MesesADevengar;
                dto.MesesPendientes = 0;
                dto.PorcentajeDevengado = c.MesesADevengar > 0 ? 100m : 0m;
                dto.PorcentajePendiente = 0m;
                dto.MontoDevengado = baseDevengable;
                dto.MontoPendiente = 0m;
                dto.TieneSaldoPendiente = false;
                break;

            case EstadoConvenio.Cobrado:
            {
                // Conteo detenido en la fecha de corte; se cobró el pendiente congelado.
                var corte = c.FechaCorte?.Date ?? Hoy();
                var (trans, pend, pctD, pctP) = Devengo(c, corte);
                dto.MesesTranscurridos = trans;
                dto.MesesPendientes = pend;
                dto.PorcentajeDevengado = pctD;
                dto.PorcentajePendiente = pctP;
                dto.MontoDevengado = Round(baseDevengable - (c.MontoCongelado ?? PendienteEn(c, corte)));
                dto.MontoCobrado = c.MontoCongelado ?? PendienteEn(c, corte);
                dto.MontoPendiente = 0m; // ya cobrado
                dto.TieneSaldoPendiente = false;
                break;
            }

            case EstadoConvenio.Anulado:
            {
                // Se conserva el valor pendiente a la fecha de corte.
                var corte = c.FechaCorte?.Date ?? Hoy();
                var (trans, pend, pctD, pctP) = Devengo(c, corte);
                dto.MesesTranscurridos = trans;
                dto.MesesPendientes = pend;
                dto.PorcentajeDevengado = pctD;
                dto.PorcentajePendiente = pctP;
                var conservado = c.MontoCongelado ?? PendienteEn(c, corte);
                dto.MontoDevengado = Round(baseDevengable - conservado);
                dto.MontoPendiente = conservado;
                dto.TieneSaldoPendiente = conservado > 0m;
                break;
            }

            case EstadoConvenio.Vigente:
            default:
            {
                var (trans, pend, pctD, pctP) = Devengo(c, Hoy());
                dto.MesesTranscurridos = trans;
                dto.MesesPendientes = pend;
                dto.PorcentajeDevengado = pctD;
                dto.PorcentajePendiente = pctP;
                dto.MontoDevengado = c.MesesADevengar > 0 ? Round(baseDevengable * trans / c.MesesADevengar) : baseDevengable;
                dto.MontoPendiente = PendienteEn(c, Hoy());
                dto.TieneSaldoPendiente = dto.MontoPendiente > 0m;
                break;
            }
        }

        return dto;
    }

    /// <summary>Devengo (meses transcurridos/pendientes y %) a una fecha dada.</summary>
    private static (int trans, int pend, decimal pctDev, decimal pctPend) Devengo(Convenio c, DateTime at)
    {
        if (c.MesesADevengar <= 0) return (0, 0, 0m, 0m);
        var trans = MesesTranscurridos(c.Fecha.Date, at);
        if (trans < 0) trans = 0;
        if (trans > c.MesesADevengar) trans = c.MesesADevengar;
        var pend = c.MesesADevengar - trans;
        var pctDev = Round((decimal)trans / c.MesesADevengar * 100m);
        var pctPend = Round((decimal)pend / c.MesesADevengar * 100m);
        return (trans, pend, pctDev, pctPend);
    }

    /// <summary>Monto pendiente (proporcional no devengado) a una fecha dada. 0 si no aplica devengo.</summary>
    private static decimal PendienteEn(Convenio c, DateTime at)
    {
        if (c.MesesADevengar <= 0) return 0m;
        var baseDevengable = c.Items.Where(i => i.Devengable).Sum(i => i.Valor);
        var (_, pend, _, _) = Devengo(c, at);
        return Round(baseDevengable * pend / c.MesesADevengar);
    }

    private static decimal Round(decimal v) => Math.Round(v, 2);

    private static int MesesTranscurridos(DateTime desde, DateTime hasta)
    {
        var m = (hasta.Year - desde.Year) * 12 + (hasta.Month - desde.Month);
        if (hasta.Day < desde.Day) m--;
        return m;
    }

    public static EstadoConvenio ParseEstado(string? s) => (s?.Trim().ToLowerInvariant()) switch
    {
        "devengado" => EstadoConvenio.Devengado,
        "cobrado" => EstadoConvenio.Cobrado,
        "anulado" => EstadoConvenio.Anulado,
        "vigente" or null or "" => EstadoConvenio.Vigente,
        _ => throw new ConvenioValidacionException("Estado inválido (Vigente, Devengado, Cobrado o Anulado)."),
    };

    /// <summary>Copia el request a la entidad y gestiona la congelación al pasar a Cobrado/Anulado.</summary>
    public static void Apply(Convenio c, ConvenioRequest req)
    {
        var titulo = (req.Titulo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ConvenioValidacionException("El título del convenio es obligatorio.");
        if (!MesesPermitidos.Contains(req.MesesADevengar))
            throw new ConvenioValidacionException("Meses a devengar inválido (use 0, 12, 24 o 36).");

        c.Titulo = titulo;
        c.Descripcion = Clean(req.Descripcion);
        c.Tipo = Clean(req.Tipo);
        c.TipoCurso = Clean(req.TipoCurso);
        c.NombreCurso = Clean(req.NombreCurso);
        c.Marca = Clean(req.Marca);
        c.SolicitadoPor = Clean(req.SolicitadoPor);
        c.AutorizadoPor = Clean(req.AutorizadoPor);
        c.Fecha = ParseDate(req.Fecha);
        c.MesesADevengar = req.MesesADevengar;

        c.Items.Clear();
        foreach (var it in req.Items ?? new List<ConvenioItemRequest>())
        {
            var tipo = (it.Tipo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(tipo))
                throw new ConvenioValidacionException("Cada ítem de costo requiere un tipo.");
            if (it.Valor < 0)
                throw new ConvenioValidacionException("El valor de un ítem no puede ser negativo.");
            // No fijamos el Id: EF lo genera al insertar. Pre-asignarlo en un padre ya tracked
            // hace que EF lo trate como existente (UPDATE) en vez de Added (INSERT).
            c.Items.Add(new ConvenioItem
            {
                ConvenioId = c.Id, Tipo = tipo, Valor = it.Valor,
                Devengable = it.Devengable, Observacion = Clean(it.Observacion),
            });
        }

        // Estado + congelación. Al entrar (por primera vez) a Cobrado/Anulado se fija la fecha de
        // corte y el monto pendiente de ese momento. Al volver a Vigente/Devengado se libera.
        var nuevoEstado = ParseEstado(req.Estado);
        c.Estado = nuevoEstado;
        if (nuevoEstado is EstadoConvenio.Cobrado or EstadoConvenio.Anulado)
        {
            if (c.FechaCorte is null)
            {
                c.FechaCorte = Hoy();
                c.MontoCongelado = PendienteEn(c, c.FechaCorte.Value);
            }
        }
        else
        {
            c.FechaCorte = null;
            c.MontoCongelado = null;
        }
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static DateTime ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ConvenioValidacionException("La fecha del convenio es obligatoria (yyyy-MM-dd).");
        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
        throw new ConvenioValidacionException("La fecha no es válida (formato yyyy-MM-dd).");
    }
}
