using System.Globalization;
using System.Text;
using Capacitaciones.Application.Common;
using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Mapeo, clasificación y cálculo de devengo del módulo Convenios (según
/// <see cref="EstadoConvenio"/>). La base de devengo es <see cref="Convenio.ValorAsumidoEmpresa"/>
/// y el conteo se ancla en <see cref="Convenio.FechaIngreso"/> (fallback a <see cref="Convenio.Fecha"/>).</summary>
public static class ConvenioMapper
{
    public static readonly int[] MesesPermitidos = { 0, 12, 24, 36 };

    // Clasificaciones (rigen plazo y modalidad de reintegro).
    public const string ClasifCursos = "Cursos y capacitaciones";
    public const string ClasifCertificaciones = "Certificaciones y exámenes";
    public const string ClasifDiplomados = "Diplomados o programas especializados";
    public const string ClasifRevisar = "Revisar";

    public const string ModalidadProporcional = "Reintegro proporcional mensual";
    public const string ModalidadEscalonado = "Reintegro escalonado especial";

    /// <summary>Umbral mínimo de valor para que exista obligación de reintegro.</summary>
    public const decimal UmbralMinimo = 60m;

    public static DateTime Hoy() => EcuadorTime.FromUtc(DateTime.UtcNow).Date;

    public static ConvenioDto ToDto(Convenio c)
    {
        var dto = new ConvenioDto
        {
            Id = c.Id,
            NumeroRegistro = c.NumeroRegistro,
            CodigoRegistro = c.NumeroRegistro is int n ? IConvenioNumeracionService.Format(n) : string.Empty,
            CedulaColaborador = c.CedulaColaborador,
            NombreColaborador = c.NombreColaborador,
            OrigenColaborador = c.OrigenColaborador,
            CargoColaborador = c.CargoColaborador,
            AreaColaborador = c.AreaColaborador,
            EmpresaColaborador = c.EmpresaColaborador,
            GeneroColaborador = c.GeneroColaborador,
            CentroCostos = c.CentroCostos,
            JefeInmediato = c.JefeInmediato,
            RelacionLaboral = c.RelacionLaboral,
            FechaIngreso = c.FechaIngreso,
            FechaFirma = c.FechaFirma,
            Titulo = c.Titulo,
            Descripcion = c.Descripcion,
            Tipo = c.Tipo,
            TipoCurso = c.TipoCurso,
            NombreCurso = c.NombreCurso,
            Marca = c.Marca,
            FechaInicioCurso = c.FechaInicioCurso,
            FechaFinCurso = c.FechaFinCurso,
            Horas = c.Horas,
            Resultado = c.Resultado,
            ConvenioFirmado = c.ConvenioFirmado,
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

        var baseDev = c.ValorAsumidoEmpresa;
        dto.MontoTotal = c.Items.Sum(i => i.Valor);
        dto.MontoDevengable = baseDev;
        dto.ValorAsumidoEmpresa = baseDev;

        var clasif = Clasificar(c.Tipo);
        dto.Clasificacion = clasif;
        dto.ModalidadReintegro = EsEscalonado(clasif) ? ModalidadEscalonado : ModalidadProporcional;
        var (plazoSug, plazoTxt) = PlazoSugerido(clasif, baseDev);
        dto.PlazoSugerido = plazoSug;
        dto.PlazoSugeridoTexto = plazoTxt;

        var aplica = AplicaDevengo(c);
        dto.AplicaDevengo = aplica;
        dto.FechaDevengable100 = aplica ? DevengoStart(c).AddMonths(c.MesesADevengar) : null;

        switch (c.Estado)
        {
            case EstadoConvenio.Devengado:
                // Cumplió el plazo sin cobro: devengado 100%, no se traslada costo.
                dto.MesesTranscurridos = c.MesesADevengar;
                dto.MesesPendientes = 0;
                dto.PorcentajeDevengado = aplica ? 100m : 0m;
                dto.PorcentajePendiente = 0m;
                dto.MontoDevengado = baseDev;
                dto.MontoPendiente = 0m;
                dto.TieneSaldoPendiente = false;
                break;

            case EstadoConvenio.Cobrado:
            {
                var corte = c.FechaCorte?.Date ?? Hoy();
                var (trans, pend, pctD, pctP) = DevengoDisplay(c, corte);
                var cobrado = c.MontoCongelado ?? PendienteEn(c, corte);
                dto.MesesTranscurridos = trans;
                dto.MesesPendientes = pend;
                dto.PorcentajeDevengado = pctD;
                dto.PorcentajePendiente = pctP;
                dto.MontoDevengado = Round(baseDev - cobrado);
                dto.MontoCobrado = cobrado;
                dto.MontoPendiente = 0m; // ya cobrado
                dto.TieneSaldoPendiente = false;
                break;
            }

            case EstadoConvenio.Anulado:
            {
                var corte = c.FechaCorte?.Date ?? Hoy();
                var (trans, pend, pctD, pctP) = DevengoDisplay(c, corte);
                var conservado = c.MontoCongelado ?? PendienteEn(c, corte);
                dto.MesesTranscurridos = trans;
                dto.MesesPendientes = pend;
                dto.PorcentajeDevengado = pctD;
                dto.PorcentajePendiente = pctP;
                dto.MontoDevengado = Round(baseDev - conservado);
                dto.MontoPendiente = conservado;
                dto.TieneSaldoPendiente = conservado > 0m;
                break;
            }

            case EstadoConvenio.Vigente:
            default:
            {
                var (trans, pend, pctD, pctP) = DevengoDisplay(c, Hoy());
                var pendiente = PendienteEn(c, Hoy());
                dto.MesesTranscurridos = trans;
                dto.MesesPendientes = pend;
                dto.PorcentajeDevengado = pctD;
                dto.PorcentajePendiente = pctP;
                dto.MontoDevengado = Round(baseDev - pendiente);
                dto.MontoPendiente = pendiente;
                dto.TieneSaldoPendiente = pendiente > 0m;
                break;
            }
        }

        return dto;
    }

    // ----- Clasificación / plazo / modalidad -----

    /// <summary>Clasifica el convenio a partir del <c>Tipo</c> (tipo de evento formativo).</summary>
    public static string Clasificar(string? tipo)
    {
        var t = Normalize(tipo);
        if (t is "examen de certificacion" or "certificacion" or "material de estudio")
            return ClasifCertificaciones;
        if (t == "curso o capacitacion")
            return ClasifCursos;
        if (t is "diplomado" or "programa especializado")
            return ClasifDiplomados;
        return ClasifRevisar;
    }

    public static bool EsEscalonado(string clasif) => clasif == ClasifCertificaciones;

    /// <summary>Plazo sugerido (meses) según clasificación y valor base. Devuelve (-1, "Anexo especial")
    /// para montos altos sin tramo y (0, "N/A") si el valor está bajo el umbral.</summary>
    public static (int meses, string texto) PlazoSugerido(string clasif, decimal valor)
    {
        if (valor < UmbralMinimo) return (0, "N/A");
        switch (clasif)
        {
            case ClasifCertificaciones:
                return (36, "36 meses");
            case ClasifCursos:
                if (valor <= 500m) return (12, "12 meses");
                if (valor <= 1500m) return (24, "24 meses");
                if (valor <= 4000m) return (36, "36 meses");
                return (-1, "Anexo especial");
            case ClasifDiplomados:
                if (valor <= 1500m) return (24, "24 meses");
                if (valor <= 4000m) return (36, "36 meses");
                return (-1, "Anexo especial");
            default:
                return (0, "Revisar");
        }
    }

    // ----- Devengo -----

    /// <summary>Ancla del devengo: fecha de ingreso del colaborador (fallback a la fecha del convenio).</summary>
    private static DateTime DevengoStart(Convenio c) => (c.FechaIngreso ?? c.Fecha).Date;

    private static bool AplicaDevengo(Convenio c) => c.MesesADevengar > 0 && c.ValorAsumidoEmpresa >= UmbralMinimo;

    /// <summary>Monto pendiente (no devengado) a una fecha dada, según la modalidad. 0 si no aplica.</summary>
    public static decimal PendienteEn(Convenio c, DateTime at)
    {
        if (!AplicaDevengo(c)) return 0m;
        var baseDev = c.ValorAsumidoEmpresa;
        var meses = MesesTranscurridos(DevengoStart(c), at);
        if (meses < 0) meses = 0;

        if (EsEscalonado(Clasificar(c.Tipo)))
        {
            // Reintegro escalonado: el pendiente decae por tramos de antigüedad.
            var pct = meses <= 12 ? 100m : meses <= 24 ? 50m : meses <= 36 ? 25m : 0m;
            return Round(baseDev * pct / 100m);
        }

        // Proporcional mensual sobre el plazo (MesesADevengar).
        if (meses > c.MesesADevengar) meses = c.MesesADevengar;
        var pend = c.MesesADevengar - meses;
        return Round(baseDev * pend / c.MesesADevengar);
    }

    /// <summary>Valores de devengo para mostrar (meses transcurridos/pendientes y %), a una fecha.</summary>
    private static (int trans, int pend, decimal pctDev, decimal pctPend) DevengoDisplay(Convenio c, DateTime at)
    {
        if (!AplicaDevengo(c)) return (0, 0, 0m, 0m);
        var meses = MesesTranscurridos(DevengoStart(c), at);
        if (meses < 0) meses = 0;

        if (EsEscalonado(Clasificar(c.Tipo)))
        {
            var trans = Math.Min(meses, 36);
            var pendAmt = PendienteEn(c, at);
            var baseDev = c.ValorAsumidoEmpresa;
            var pctPend = baseDev > 0m ? Round(pendAmt / baseDev * 100m) : 0m;
            return (trans, Math.Max(0, 36 - trans), Round(100m - pctPend), pctPend);
        }

        var t = Math.Min(meses, c.MesesADevengar);
        var p = c.MesesADevengar - t;
        return (t, p, Round((decimal)t / c.MesesADevengar * 100m), Round((decimal)p / c.MesesADevengar * 100m));
    }

    private static decimal Round(decimal v) => Math.Round(v, 2);

    /// <summary>Meses completos entre dos fechas (mínimo 0). Helper público para liquidaciones.</summary>
    public static int MesesEntre(DateTime desde, DateTime hasta)
    {
        var m = MesesTranscurridos(desde.Date, hasta.Date);
        return m < 0 ? 0 : m;
    }

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
        if (req.ValorAsumidoEmpresa < 0m)
            throw new ConvenioValidacionException("El valor asumido por la empresa no puede ser negativo.");

        c.Titulo = titulo;
        c.Descripcion = Clean(req.Descripcion);
        c.Tipo = Clean(req.Tipo);
        c.TipoCurso = Clean(req.TipoCurso);
        c.NombreCurso = Clean(req.NombreCurso);
        c.Marca = Clean(req.Marca);
        c.SolicitadoPor = Clean(req.SolicitadoPor);
        c.AutorizadoPor = Clean(req.AutorizadoPor);
        c.Fecha = ParseDate(req.Fecha, "La fecha del convenio es obligatoria (yyyy-MM-dd).");
        c.ValorAsumidoEmpresa = req.ValorAsumidoEmpresa;
        c.MesesADevengar = req.MesesADevengar;

        // Snapshot manual del colaborador.
        c.CentroCostos = Clean(req.CentroCostos);
        c.JefeInmediato = Clean(req.JefeInmediato);
        c.RelacionLaboral = Clean(req.RelacionLaboral);
        c.FechaIngreso = ParseDate(req.FechaIngreso, "La fecha de ingreso es obligatoria (yyyy-MM-dd).");
        c.FechaFirma = ParseOptionalDate(req.FechaFirma);

        // Detalle del evento.
        c.FechaInicioCurso = ParseOptionalDate(req.FechaInicioCurso);
        c.FechaFinCurso = ParseOptionalDate(req.FechaFinCurso);
        if (req.Horas is < 0) throw new ConvenioValidacionException("Las horas no pueden ser negativas.");
        c.Horas = req.Horas;
        c.Resultado = Clean(req.Resultado);
        c.ConvenioFirmado = req.ConvenioFirmado;

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

    /// <summary>Normaliza un texto a minúsculas sin acentos para comparaciones de catálogo.</summary>
    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static DateTime ParseDate(string? value, string requiredMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ConvenioValidacionException(requiredMessage);
        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
        throw new ConvenioValidacionException("La fecha no es válida (formato yyyy-MM-dd).");
    }

    private static DateTime? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d.Date, DateTimeKind.Utc);
        throw new ConvenioValidacionException("La fecha no es válida (formato yyyy-MM-dd).");
    }
}
