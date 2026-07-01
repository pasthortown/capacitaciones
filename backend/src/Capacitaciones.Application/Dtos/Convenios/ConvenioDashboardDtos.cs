namespace Capacitaciones.Application.Dtos.Convenios;

// ===== Dashboard en pantalla (datos agregados, solo con datos propios de Convenios) =====
public class DashboardConveniosResumenDto
{
    public DateTime FechaCorte { get; set; }
    // KPIs
    public int TotalConvenios { get; set; }
    public int TotalPersonas { get; set; }
    public decimal TotalAsumido { get; set; }
    public decimal TotalDevengado { get; set; }
    public decimal TotalPorDevengar { get; set; }
    public decimal TotalHoras { get; set; }
    public decimal CostoPromedioPersona { get; set; }
    public int ConveniosFirmados { get; set; }

    /// <summary>Cortes por dimensión (estado, marca, género, área, cargo, empresa, clasificación, resultado).</summary>
    public List<DashboardDimensionDto> Dimensiones { get; set; } = new();
    /// <summary>Inversión por mes (eje temporal).</summary>
    public List<DashboardMesDto> PorMes { get; set; } = new();
    /// <summary>Detalle por curso (también usado por el PDF).</summary>
    public List<DashboardCursoDto> Cursos { get; set; } = new();
}

/// <summary>Una dimensión de agrupación con sus grupos (para un gráfico de barras).</summary>
public class DashboardDimensionDto
{
    public string Clave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public List<DashboardGrupoDto> Grupos { get; set; } = new();
}

public class DashboardGrupoDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int Personas { get; set; }
    public decimal Inversion { get; set; }
    public decimal Devengado { get; set; }
    public decimal PorDevengar { get; set; }
}

public class DashboardMesDto
{
    public string Mes { get; set; } = string.Empty;
    public decimal Inversion { get; set; }
    public int Convenios { get; set; }
}

// ===== Liquidación por desvinculación (reintegro a una fecha de salida) =====
public class LiquidacionColaboradorDto
{
    public string Cedula { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Cargo { get; set; }
    public string? Area { get; set; }
    public string? Empresa { get; set; }
    public string? Origen { get; set; }
    public DateTime FechaSalida { get; set; }
    public List<LiquidacionConvenioDto> Convenios { get; set; } = new();
    public decimal TotalReintegro { get; set; }
}

public class LiquidacionConvenioDto
{
    public Guid Id { get; set; }
    public string CodigoRegistro { get; set; } = string.Empty;
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }
    public string Clasificacion { get; set; } = string.Empty;
    public string ModalidadReintegro { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime? FechaIngreso { get; set; }
    public decimal ValorAsumidoEmpresa { get; set; }
    public int MesesADevengar { get; set; }
    public int MesesTranscurridosASalida { get; set; }
    /// <summary>Monto que el colaborador debe reintegrar si sale en la fecha indicada.</summary>
    public decimal MontoReintegro { get; set; }
}
