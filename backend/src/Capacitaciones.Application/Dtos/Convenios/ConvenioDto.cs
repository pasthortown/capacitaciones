namespace Capacitaciones.Application.Dtos.Convenios;

/// <summary>
/// Convenio para listado/detalle e impresión. Los campos de devengo son <b>calculados</b> según
/// el <see cref="Estado"/> y la fecha actual (o la de corte para Cobrado/Anulado).
/// </summary>
public class ConvenioDto
{
    public Guid Id { get; set; }
    /// <summary>Número de registro secuencial (null si aún sin numerar).</summary>
    public int? NumeroRegistro { get; set; }
    /// <summary>Código del anexo <c>GIC-EC-REG-###</c> (vacío si sin numerar).</summary>
    public string CodigoRegistro { get; set; } = string.Empty;

    public string CedulaColaborador { get; set; } = string.Empty;
    public string NombreColaborador { get; set; } = string.Empty;
    public string? OrigenColaborador { get; set; }
    public string? CargoColaborador { get; set; }
    /// <summary>Área del colaborador = Departamento.</summary>
    public string? AreaColaborador { get; set; }
    public string? EmpresaColaborador { get; set; }
    public string? GeneroColaborador { get; set; }
    public string? CentroCostos { get; set; }
    public string? JefeInmediato { get; set; }
    public string? RelacionLaboral { get; set; }
    public DateTime? FechaIngreso { get; set; }
    public DateTime? FechaFirma { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Tipo { get; set; }
    public string? TipoCurso { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }

    public DateTime? FechaInicioCurso { get; set; }
    public DateTime? FechaFinCurso { get; set; }
    public decimal? Horas { get; set; }
    public string? Resultado { get; set; }
    public bool ConvenioFirmado { get; set; }

    public string? SolicitadoPor { get; set; }
    public string? AutorizadoPor { get; set; }
    public DateTime Fecha { get; set; }
    public int MesesADevengar { get; set; }
    /// <summary>"Vigente" | "Devengado" | "Cobrado" | "Anulado".</summary>
    public string Estado { get; set; } = "Vigente";
    public DateTime? FechaCorte { get; set; }
    public bool Activo { get; set; }

    public List<ConvenioItemDto> Items { get; set; } = new();
    public List<ConvenioAnexoDto> Anexos { get; set; } = new();
    public decimal MontoTotal { get; set; }
    /// <summary>Base de devengo = Valor asumido por la empresa.</summary>
    public decimal MontoDevengable { get; set; }
    /// <summary>Valor asumido por la empresa (= base de devengo). Alias explícito para el UI.</summary>
    public decimal ValorAsumidoEmpresa { get; set; }

    // --- Clasificación automática (derivada del Tipo) ---
    /// <summary>Cursos y capacitaciones | Certificaciones y exámenes | Diplomados o programas especializados | Revisar.</summary>
    public string Clasificacion { get; set; } = string.Empty;
    /// <summary>Reintegro proporcional mensual | Reintegro escalonado especial.</summary>
    public string ModalidadReintegro { get; set; } = string.Empty;
    /// <summary>Plazo sugerido en meses según clasificación y valor (0 = N/A si valor &lt; $60).</summary>
    public int PlazoSugerido { get; set; }
    /// <summary>Etiqueta del plazo sugerido (ej. "36 meses", "N/A", "Anexo especial").</summary>
    public string PlazoSugeridoTexto { get; set; } = string.Empty;

    // --- Devengo ---
    public bool AplicaDevengo { get; set; }
    public DateTime? FechaDevengable100 { get; set; }
    public int MesesTranscurridos { get; set; }
    public int MesesPendientes { get; set; }
    public decimal PorcentajeDevengado { get; set; }
    public decimal PorcentajePendiente { get; set; }
    public decimal MontoDevengado { get; set; }
    /// <summary>Deuda actual del colaborador (proporcional pendiente en Vigente; conservado en Anulado; 0 en Devengado/Cobrado).</summary>
    public decimal MontoPendiente { get; set; }
    /// <summary>Valor que se cobró al colaborador (solo cuando Estado=Cobrado).</summary>
    public decimal? MontoCobrado { get; set; }
    /// <summary>True si el convenio tiene saldo por cobrar (relevante para el historial de cobro).</summary>
    public bool TieneSaldoPendiente { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}
