namespace Capacitaciones.Application.Dtos.Convenios;

// ===== Dashboard en pantalla (datos agregados) =====
public class DashboardConveniosResumenDto
{
    public DateTime FechaCorte { get; set; }
    public int TotalConvenios { get; set; }
    public decimal TotalAsumido { get; set; }
    public decimal TotalDevengado { get; set; }
    public decimal TotalPorDevengar { get; set; }
    public List<DashboardEstadoDto> PorEstado { get; set; } = new();
    public List<DashboardMarcaDto> PorMarca { get; set; } = new();
    public List<DashboardCursoDto> Cursos { get; set; } = new();
}

public class DashboardEstadoDto
{
    public string Estado { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal MontoAsumido { get; set; }
    public decimal MontoPendiente { get; set; }
}

public class DashboardMarcaDto
{
    public string Marca { get; set; } = string.Empty;
    public int Convenios { get; set; }
    public int Personas { get; set; }
    public decimal Inversion { get; set; }
    public decimal Devengado { get; set; }
    public decimal PorDevengar { get; set; }
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
    public string Titulo { get; set; } = string.Empty;
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
