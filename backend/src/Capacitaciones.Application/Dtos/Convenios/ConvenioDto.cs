namespace Capacitaciones.Application.Dtos.Convenios;

/// <summary>
/// Convenio para listado/detalle e impresión. Los campos de devengo son <b>calculados</b> según
/// el <see cref="Estado"/> y la fecha actual (o la de corte para Cobrado/Anulado).
/// </summary>
public class ConvenioDto
{
    public Guid Id { get; set; }
    public string CedulaColaborador { get; set; } = string.Empty;
    public string NombreColaborador { get; set; } = string.Empty;
    public string? OrigenColaborador { get; set; }
    public string? CargoColaborador { get; set; }
    public string? AreaColaborador { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Tipo { get; set; }
    public string? TipoCurso { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }
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
    public decimal MontoDevengable { get; set; }

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
