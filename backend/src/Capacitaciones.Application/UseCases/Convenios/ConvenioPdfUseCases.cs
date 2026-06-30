using System.Globalization;
using Capacitaciones.Application.Dtos.Certificados;
using Capacitaciones.Application.Dtos.Convenios;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Asistentes;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Convenios;

/// <summary>Utilidades compartidas por los casos de uso de PDF de convenios.</summary>
internal static class ConvenioPdfHelpers
{
    public static string? Fmt(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static string Fmt(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static CertificadoDescargaDto ReadOutput(string? outputDir, string ruta)
    {
        var filename = ExtractFilename(ruta);
        var dir = string.IsNullOrWhiteSpace(outputDir) ? "/output" : outputDir;
        var full = Path.Combine(dir, filename);
        if (!File.Exists(full))
            throw new InvalidOperationException(
                $"El PDF no fue encontrado en '{full}' tras la emisión. Verifica el volumen '/output'.");
        return new CertificadoDescargaDto(new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read), filename);
    }

    private static string ExtractFilename(string ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return string.Empty;
        var idx = ruta.LastIndexOfAny(new[] { '/', '\\' });
        return idx >= 0 && idx < ruta.Length - 1 ? ruta[(idx + 1)..] : ruta;
    }

    public static List<ConvenioItemEmisionDto> MapItems(Convenio c) =>
        c.Items.Select(i => new ConvenioItemEmisionDto
        {
            Tipo = i.Tipo, Valor = i.Valor, Devengable = i.Devengable, Observacion = i.Observacion,
        }).ToList();
}

/// <summary>B) Genera el PDF del convenio (documento GIC-EC-ANX-01) y lo devuelve para descarga.</summary>
public class ImprimirConvenioUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IEmisorDocumentosClient _emisor;
    private readonly CertificadosOptions _options;

    public ImprimirConvenioUseCase(IConvenioRepository repo, IEmisorDocumentosClient emisor, CertificadosOptions options)
    {
        _repo = repo;
        _emisor = emisor;
        _options = options;
    }

    public async Task<CertificadoDescargaDto> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _repo.GetByIdAsync(id, ct) ?? throw new ConvenioNotFoundException(id);
        var dto = ConvenioMapper.ToDto(c);

        var req = new ConvenioImprimirRequest
        {
            Convenio = new ConvenioImprimirConvenioDto
            {
                CodigoRegistro = string.IsNullOrWhiteSpace(dto.CodigoRegistro) ? "—" : dto.CodigoRegistro,
                Titulo = c.Titulo,
                Tipo = c.Tipo,
                TipoCurso = c.TipoCurso,
                NombreCurso = c.NombreCurso,
                Marca = c.Marca,
                FechaConvenio = ConvenioPdfHelpers.Fmt(c.Fecha),
                FechaFirma = ConvenioPdfHelpers.Fmt(c.FechaFirma),
                FechaCreacion = ConvenioPdfHelpers.Fmt(c.FechaCreacion),
                FechaInicioCurso = ConvenioPdfHelpers.Fmt(c.FechaInicioCurso),
                FechaFinCurso = ConvenioPdfHelpers.Fmt(c.FechaFinCurso),
                Horas = c.Horas,
                Resultado = c.Resultado,
                Clasificacion = dto.Clasificacion,
                ModalidadReintegro = dto.ModalidadReintegro,
                PlazoTexto = dto.PlazoSugeridoTexto,
                MesesADevengar = c.MesesADevengar,
                MontoTotal = dto.MontoTotal,
                ValorAsumidoEmpresa = c.ValorAsumidoEmpresa,
                ConvenioFirmado = c.ConvenioFirmado,
            },
            Colaborador = new ConvenioImprimirColaboradorDto
            {
                Nombre = c.NombreColaborador,
                Cedula = c.CedulaColaborador,
                Cargo = c.CargoColaborador,
                Area = c.AreaColaborador,
                Empresa = c.EmpresaColaborador,
                CentroCostos = c.CentroCostos,
                JefeInmediato = c.JefeInmediato,
                RelacionLaboral = c.RelacionLaboral,
                FechaIngreso = ConvenioPdfHelpers.Fmt(c.FechaIngreso),
                Origen = c.OrigenColaborador,
            },
            Items = ConvenioPdfHelpers.MapItems(c),
            SolicitadoPor = c.SolicitadoPor,
            AutorizadoPor = c.AutorizadoPor,
        };

        var resultado = await _emisor.EmitirConvenioAsync(req, ct);
        return ConvenioPdfHelpers.ReadOutput(_options.OutputDir, resultado.Ruta);
    }
}

/// <summary>D) Genera el PDF de reporte de convenios por colaborador (montos por devengar).</summary>
public class DescargarReporteConveniosUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IEmisorDocumentosClient _emisor;
    private readonly CertificadosOptions _options;

    public DescargarReporteConveniosUseCase(IConvenioRepository repo, IEmisorDocumentosClient emisor, CertificadosOptions options)
    {
        _repo = repo;
        _emisor = emisor;
        _options = options;
    }

    public async Task<CertificadoDescargaDto> ExecuteAsync(string cedula, CancellationToken ct = default)
    {
        var ced = (cedula ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ced))
            throw new ConvenioValidacionException("La cédula es obligatoria.");

        var entidades = await _repo.ListByCedulaAsync(ced, includeInactive: false, ct);
        var dtos = entidades.Select(ConvenioMapper.ToDto)
            .Where(d => d.TieneSaldoPendiente)
            .ToList();

        var primero = entidades.FirstOrDefault();
        var col = new ConvenioImprimirColaboradorDto
        {
            Cedula = ced,
            Nombre = primero?.NombreColaborador ?? ced,
            Cargo = primero?.CargoColaborador,
            Area = primero?.AreaColaborador,
            Empresa = primero?.EmpresaColaborador,
            Origen = primero?.OrigenColaborador,
        };

        var req = new ReporteConveniosRequest
        {
            Colaborador = col,
            FechaCorte = ConvenioPdfHelpers.Fmt(ConvenioMapper.Hoy()),
            TotalPorDevengar = dtos.Sum(d => d.MontoPendiente),
            Convenios = dtos.Select(d => new ReporteConvenioDto
            {
                CodigoRegistro = d.CodigoRegistro,
                Titulo = d.Titulo,
                NombreCurso = d.NombreCurso,
                Marca = d.Marca,
                Fecha = ConvenioPdfHelpers.Fmt(d.Fecha),
                FechaIngreso = ConvenioPdfHelpers.Fmt(d.FechaIngreso),
                Estado = d.Estado,
                MontoTotal = d.MontoTotal,
                ValorAsumidoEmpresa = d.ValorAsumidoEmpresa,
                MontoDevengado = d.MontoDevengado,
                MontoPendiente = d.MontoPendiente,
                MesesADevengar = d.MesesADevengar,
                MesesTranscurridos = d.MesesTranscurridos,
                MesesPendientes = d.MesesPendientes,
                PorcentajePendiente = d.PorcentajePendiente,
                SolicitadoPor = d.SolicitadoPor,
                AutorizadoPor = d.AutorizadoPor,
                Items = d.Items.Select(i => new ConvenioItemEmisionDto
                {
                    Tipo = i.Tipo, Valor = i.Valor, Devengable = i.Devengable, Observacion = i.Observacion,
                }).ToList(),
            }).ToList(),
        };

        var resultado = await _emisor.EmitirReporteConveniosAsync(req, ct);
        return ConvenioPdfHelpers.ReadOutput(_options.OutputDir, resultado.Ruta);
    }
}

/// <summary>E) Dashboard de convenios: agregados en pantalla + PDF resumen por curso con pastel.</summary>
public class DashboardConveniosUseCase
{
    private readonly IConvenioRepository _repo;
    private readonly IEmisorDocumentosClient _emisor;
    private readonly CertificadosOptions _options;

    public DashboardConveniosUseCase(IConvenioRepository repo, IEmisorDocumentosClient emisor, CertificadosOptions options)
    {
        _repo = repo;
        _emisor = emisor;
        _options = options;
    }

    private async Task<List<ConvenioDto>> CargarActivosAsync(CancellationToken ct)
    {
        var entidades = await _repo.ListAsync(null, includeInactive: false, ct);
        return entidades.Select(ConvenioMapper.ToDto).ToList();
    }

    public async Task<DashboardConveniosResumenDto> ResumenAsync(CancellationToken ct = default)
    {
        var dtos = await CargarActivosAsync(ct);

        // Dimensión genérica: agrupa por un selector de etiqueta.
        DashboardDimensionDto Dim(string clave, string titulo, Func<ConvenioDto, string> key) => new()
        {
            Clave = clave,
            Titulo = titulo,
            Grupos = dtos.GroupBy(key).Select(g => new DashboardGrupoDto
            {
                Etiqueta = g.Key,
                Cantidad = g.Count(),
                Personas = g.Select(x => x.CedulaColaborador).Distinct().Count(),
                Inversion = g.Sum(x => x.ValorAsumidoEmpresa),
                Devengado = g.Sum(x => x.MontoDevengado),
                PorDevengar = g.Sum(x => x.MontoPendiente),
            }).OrderByDescending(x => x.Inversion).ThenBy(x => x.Etiqueta).ToList(),
        };

        static string OrGuion(string? s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
        static string Genero(string? s)
        {
            var t = (s ?? "").Trim().ToUpperInvariant();
            if (t.StartsWith("M")) return "Masculino";
            if (t.StartsWith("F")) return "Femenino";
            return "Sin especificar";
        }

        var dimensiones = new List<DashboardDimensionDto>
        {
            Dim("estado", "Por estado", d => d.Estado),
            Dim("genero", "Por género", d => Genero(d.GeneroColaborador)),
            Dim("clasificacion", "Por clasificación", d => OrGuion(d.Clasificacion, "Sin clasificar")),
            Dim("marca", "Por marca", d => OrGuion(d.Marca, "Sin marca")),
            Dim("area", "Por área / departamento", d => OrGuion(d.AreaColaborador, "Sin área")),
            Dim("cargo", "Por cargo", d => OrGuion(d.CargoColaborador, "Sin cargo")),
            Dim("empresa", "Por empresa", d => OrGuion(d.EmpresaColaborador, "Sin empresa")),
            Dim("resultado", "Por resultado", d => OrGuion(d.Resultado, "Sin resultado")),
        };

        var porMes = dtos.GroupBy(d => d.Fecha.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))
            .Select(g => new DashboardMesDto { Mes = g.Key, Inversion = g.Sum(x => x.ValorAsumidoEmpresa), Convenios = g.Count() })
            .OrderBy(m => m.Mes).ToList();

        var personas = dtos.Select(d => d.CedulaColaborador).Distinct().Count();
        var totalAsumido = dtos.Sum(d => d.ValorAsumidoEmpresa);

        return new DashboardConveniosResumenDto
        {
            FechaCorte = ConvenioMapper.Hoy(),
            TotalConvenios = dtos.Count,
            TotalPersonas = personas,
            TotalAsumido = totalAsumido,
            TotalDevengado = dtos.Sum(d => d.MontoDevengado),
            TotalPorDevengar = dtos.Sum(d => d.MontoPendiente),
            TotalHoras = dtos.Sum(d => d.Horas ?? 0m),
            CostoPromedioPersona = personas > 0 ? Math.Round(totalAsumido / personas, 2) : 0m,
            ConveniosFirmados = dtos.Count(d => d.ConvenioFirmado),
            Dimensiones = dimensiones,
            PorMes = porMes,
            Cursos = dtos.Select(MapCurso).ToList(),
        };
    }

    public async Task<CertificadoDescargaDto> PdfAsync(CancellationToken ct = default)
    {
        var dtos = await CargarActivosAsync(ct);
        var cursos = dtos.Select(MapCurso).ToList();

        var req = new DashboardConveniosRequest
        {
            FechaCorte = ConvenioPdfHelpers.Fmt(ConvenioMapper.Hoy()),
            Cursos = cursos,
            Totales = new DashboardTotalesDto
            {
                CostoTotal = cursos.Sum(c => c.CostoTotal),
                CostoAsumidoDOS = cursos.Sum(c => c.CostoAsumidoDOS),
                CostoDevengado = cursos.Sum(c => c.CostoDevengado),
                CostoPorDevengar = cursos.Sum(c => c.CostoPorDevengar),
            },
        };

        var resultado = await _emisor.EmitirDashboardConveniosAsync(req, ct);
        return ConvenioPdfHelpers.ReadOutput(_options.OutputDir, resultado.Ruta);
    }

    private static DashboardCursoDto MapCurso(ConvenioDto d) => new()
    {
        NombreCurso = string.IsNullOrWhiteSpace(d.NombreCurso) ? d.Titulo : d.NombreCurso,
        CodigoRegistro = d.CodigoRegistro,
        Colaborador = d.NombreColaborador,
        CostoTotal = d.MontoTotal,
        CostoAsumidoDOS = d.ValorAsumidoEmpresa,
        CostoDevengado = d.MontoDevengado,
        CostoPorDevengar = d.MontoPendiente,
    };
}

/// <summary>C) Liquidación por desvinculación: reintegro por convenio a una fecha de salida.</summary>
public class LiquidacionColaboradorUseCase
{
    private readonly IConvenioRepository _repo;

    public LiquidacionColaboradorUseCase(IConvenioRepository repo) => _repo = repo;

    public async Task<LiquidacionColaboradorDto> ExecuteAsync(string cedula, DateTime fechaSalida, CancellationToken ct = default)
    {
        var ced = (cedula ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ced))
            throw new ConvenioValidacionException("La cédula es obligatoria.");

        var entidades = await _repo.ListByCedulaAsync(ced, includeInactive: false, ct);
        var primero = entidades.FirstOrDefault();

        var lineas = new List<LiquidacionConvenioDto>();
        foreach (var c in entidades.Where(c => c.Estado == EstadoConvenio.Vigente))
        {
            var reintegro = ConvenioMapper.PendienteEn(c, fechaSalida.Date);
            if (reintegro <= 0m) continue;

            var clasif = ConvenioMapper.Clasificar(c.Tipo);
            var ancla = (c.FechaIngreso ?? c.Fecha).Date;
            lineas.Add(new LiquidacionConvenioDto
            {
                Id = c.Id,
                CodigoRegistro = c.NumeroRegistro is int n ? IConvenioNumeracionService.Format(n) : string.Empty,
                Titulo = c.Titulo,
                NombreCurso = c.NombreCurso,
                Marca = c.Marca,
                Clasificacion = clasif,
                ModalidadReintegro = ConvenioMapper.EsEscalonado(clasif)
                    ? ConvenioMapper.ModalidadEscalonado : ConvenioMapper.ModalidadProporcional,
                Estado = c.Estado.ToString(),
                FechaIngreso = c.FechaIngreso,
                ValorAsumidoEmpresa = c.ValorAsumidoEmpresa,
                MesesADevengar = c.MesesADevengar,
                MesesTranscurridosASalida = ConvenioMapper.MesesEntre(ancla, fechaSalida),
                MontoReintegro = reintegro,
            });
        }

        return new LiquidacionColaboradorDto
        {
            Cedula = ced,
            Nombre = primero?.NombreColaborador ?? ced,
            Cargo = primero?.CargoColaborador,
            Area = primero?.AreaColaborador,
            Empresa = primero?.EmpresaColaborador,
            Origen = primero?.OrigenColaborador,
            FechaSalida = fechaSalida.Date,
            Convenios = lineas,
            TotalReintegro = lineas.Sum(l => l.MontoReintegro),
        };
    }
}

/// <summary>Genera el PDF de liquidación por desvinculación (reusa el cálculo de
/// <see cref="LiquidacionColaboradorUseCase"/>) y lo devuelve para descarga.</summary>
public class DescargarReporteLiquidacionUseCase
{
    private readonly LiquidacionColaboradorUseCase _liquidacion;
    private readonly IEmisorDocumentosClient _emisor;
    private readonly CertificadosOptions _options;

    public DescargarReporteLiquidacionUseCase(LiquidacionColaboradorUseCase liquidacion, IEmisorDocumentosClient emisor, CertificadosOptions options)
    {
        _liquidacion = liquidacion;
        _emisor = emisor;
        _options = options;
    }

    public async Task<CertificadoDescargaDto> ExecuteAsync(string cedula, DateTime fechaSalida, CancellationToken ct = default)
    {
        var d = await _liquidacion.ExecuteAsync(cedula, fechaSalida, ct);

        var req = new LiquidacionReporteRequest
        {
            Colaborador = new ConvenioImprimirColaboradorDto
            {
                Nombre = d.Nombre, Cedula = d.Cedula, Cargo = d.Cargo,
                Area = d.Area, Empresa = d.Empresa, Origen = d.Origen,
            },
            FechaSalida = ConvenioPdfHelpers.Fmt(d.FechaSalida),
            TotalReintegro = d.TotalReintegro,
            Convenios = d.Convenios.Select(c => new LiquidacionReporteConvenioDto
            {
                CodigoRegistro = c.CodigoRegistro,
                Titulo = c.Titulo,
                NombreCurso = c.NombreCurso,
                Marca = c.Marca,
                Clasificacion = c.Clasificacion,
                ModalidadReintegro = c.ModalidadReintegro,
                Estado = c.Estado,
                FechaIngreso = ConvenioPdfHelpers.Fmt(c.FechaIngreso),
                ValorAsumidoEmpresa = c.ValorAsumidoEmpresa,
                MesesADevengar = c.MesesADevengar,
                MesesTranscurridosASalida = c.MesesTranscurridosASalida,
                MontoReintegro = c.MontoReintegro,
            }).ToList(),
        };

        var resultado = await _emisor.EmitirLiquidacionConveniosAsync(req, ct);
        return ConvenioPdfHelpers.ReadOutput(_options.OutputDir, resultado.Ruta);
    }
}
