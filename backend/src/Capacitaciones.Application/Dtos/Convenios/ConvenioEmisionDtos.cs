namespace Capacitaciones.Application.Dtos.Convenios;

// ----- Ítem reutilizable en los payloads del emisor -----
public class ConvenioItemEmisionDto
{
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public bool Devengable { get; set; }
    public string? Observacion { get; set; }
}

// ===== B) POST /emitir/convenio (documento GIC-EC-ANX-01) =====
public class ConvenioImprimirRequest
{
    public ConvenioImprimirConvenioDto Convenio { get; set; } = new();
    public ConvenioImprimirColaboradorDto Colaborador { get; set; } = new();
    public List<ConvenioItemEmisionDto> Items { get; set; } = new();
    public string? SolicitadoPor { get; set; }
    public string? AutorizadoPor { get; set; }
}

public class ConvenioImprimirConvenioDto
{
    public string CodigoRegistro { get; set; } = string.Empty;
    public string CodigoFormato { get; set; } = "GIC-EC-ANX-01";
    public string Version { get; set; } = "v1";
    public string Titulo { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public string? TipoCurso { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }
    public string? FechaConvenio { get; set; }
    public string? FechaFirma { get; set; }
    public string? FechaCreacion { get; set; }
    public string? FechaInicioCurso { get; set; }
    public string? FechaFinCurso { get; set; }
    public decimal? Horas { get; set; }
    public string? Resultado { get; set; }
    public string Clasificacion { get; set; } = string.Empty;
    public string ModalidadReintegro { get; set; } = string.Empty;
    public string PlazoTexto { get; set; } = string.Empty;
    public int MesesADevengar { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal ValorAsumidoEmpresa { get; set; }
    public bool ConvenioFirmado { get; set; }
}

public class ConvenioImprimirColaboradorDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? Area { get; set; }
    public string? Empresa { get; set; }
    public string? CentroCostos { get; set; }
    public string? JefeInmediato { get; set; }
    public string? RelacionLaboral { get; set; }
    public string? FechaIngreso { get; set; }
    public string? Origen { get; set; }
}

// ===== D) POST /emitir/reporte-convenios (historial por colaborador) =====
public class ReporteConveniosRequest
{
    public ConvenioImprimirColaboradorDto Colaborador { get; set; } = new();
    public string FechaCorte { get; set; } = string.Empty;
    public List<ReporteConvenioDto> Convenios { get; set; } = new();
    public decimal TotalPorDevengar { get; set; }
}

public class ReporteConvenioDto
{
    public string CodigoRegistro { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }
    public string? Fecha { get; set; }
    public string? FechaIngreso { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public decimal ValorAsumidoEmpresa { get; set; }
    public decimal MontoDevengado { get; set; }
    public decimal MontoPendiente { get; set; }
    public int MesesADevengar { get; set; }
    public int MesesTranscurridos { get; set; }
    public int MesesPendientes { get; set; }
    public decimal PorcentajePendiente { get; set; }
    public string? SolicitadoPor { get; set; }
    public string? AutorizadoPor { get; set; }
    public List<ConvenioItemEmisionDto> Items { get; set; } = new();
}

// ===== E) POST /emitir/dashboard-convenios (resumen por curso + pastel) =====
public class DashboardConveniosRequest
{
    public string FechaCorte { get; set; } = string.Empty;
    public List<DashboardCursoDto> Cursos { get; set; } = new();
    public DashboardTotalesDto Totales { get; set; } = new();
}

public class DashboardCursoDto
{
    public string? NombreCurso { get; set; }
    public string? CodigoRegistro { get; set; }
    public string? Colaborador { get; set; }
    public decimal CostoTotal { get; set; }
    public decimal CostoAsumidoDOS { get; set; }
    public decimal CostoDevengado { get; set; }
    public decimal CostoPorDevengar { get; set; }
}

public class DashboardTotalesDto
{
    public decimal CostoTotal { get; set; }
    public decimal CostoAsumidoDOS { get; set; }
    public decimal CostoDevengado { get; set; }
    public decimal CostoPorDevengar { get; set; }
}
