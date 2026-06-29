namespace Capacitaciones.Domain.Entities;

/// <summary>
/// Estado de negocio del convenio.
/// <list type="bullet">
/// <item><c>Vigente</c>: en devengo; el colaborador aún no termina de devengar.</item>
/// <item><c>Devengado</c>: cumplió el plazo sin cobro; no se traslada costo (pendiente 0).</item>
/// <item><c>Cobrado</c>: el colaborador salió y se le cobró; se detiene el conteo y se congela el
/// valor que se debió cobrar (deuda actual 0).</item>
/// <item><c>Anulado</c>: anulado por motivo justificado, conservando el valor pendiente a la fecha de corte.</item>
/// </list>
/// </summary>
public enum EstadoConvenio
{
    Vigente = 1,
    Devengado = 2,
    Cobrado = 3,
    Anulado = 4,
}

/// <summary>
/// Convenio (módulo Entrenamiento) asociado <b>obligatoriamente</b> a un colaborador (interno de
/// DOS o externo), referenciado por <see cref="CedulaColaborador"/> + snapshot de nombre.
///
/// El costo se detalla en <see cref="Items"/>; el monto total es la suma de ítems y la base de
/// devengo es la suma de ítems devengables. El valor se devenga proporcional mes a mes a lo largo
/// de <see cref="MesesADevengar"/> (0 = no aplica). El <see cref="Estado"/> rige cómo se calcula lo
/// adeudado; al pasar a <c>Cobrado</c>/<c>Anulado</c> se congela el pendiente en
/// <see cref="MontoCongelado"/> con <see cref="FechaCorte"/>.
///
/// <see cref="Activo"/> es baja lógica (visibilidad), independiente del estado de negocio.
/// </summary>
public class Convenio
{
    public Guid Id { get; set; }

    public string CedulaColaborador { get; set; } = string.Empty;
    public string NombreColaborador { get; set; } = string.Empty;
    public string? OrigenColaborador { get; set; }
    /// <summary>Cargo del colaborador al crear/editar (snapshot, para impresión).</summary>
    public string? CargoColaborador { get; set; }
    /// <summary>Área del colaborador al crear/editar (snapshot, para impresión).</summary>
    public string? AreaColaborador { get; set; }

    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Tipo { get; set; }
    public string? TipoCurso { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }

    /// <summary>Quién solicitó el convenio (texto libre).</summary>
    public string? SolicitadoPor { get; set; }
    /// <summary>Quién autorizó el convenio (texto libre).</summary>
    public string? AutorizadoPor { get; set; }

    /// <summary>Fecha del convenio (inicio del devengo).</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Meses a devengar para el 100% (12, 24, 36) o 0 = no aplica devengo.</summary>
    public int MesesADevengar { get; set; }

    public EstadoConvenio Estado { get; set; } = EstadoConvenio.Vigente;

    /// <summary>Fecha de corte cuando el convenio pasó a Cobrado/Anulado (detiene el conteo).</summary>
    public DateTime? FechaCorte { get; set; }

    /// <summary>Valor pendiente congelado al corte (lo que se debió cobrar / lo conservado al anular).</summary>
    public decimal? MontoCongelado { get; set; }

    /// <summary>Detalle de costos del convenio.</summary>
    public List<ConvenioItem> Items { get; set; } = new();

    /// <summary>Anexos del convenio (convenio firmado, formulario de cobro firmado, etc.). 0..N archivos.</summary>
    public List<ConvenioAnexo> Anexos { get; set; } = new();

    public bool Activo { get; set; } = true;

    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
}

/// <summary>Ítem de costo de un convenio. Si <see cref="Devengable"/> es false, su valor no se
/// traslada al colaborador.</summary>
public class ConvenioItem
{
    public Guid Id { get; set; }
    public Guid ConvenioId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public bool Devengable { get; set; } = true;
    public string? Observacion { get; set; }
}

/// <summary>Archivo adjunto a un convenio (convenio firmado, formulario de cobro firmado, etc.).</summary>
public class ConvenioAnexo
{
    public Guid Id { get; set; }
    public Guid ConvenioId { get; set; }
    /// <summary>Nombre original del archivo subido.</summary>
    public string NombreOriginal { get; set; } = string.Empty;
    /// <summary>Nombre físico en el volumen (<c>{guid}.{ext}</c>).</summary>
    public string NombreAlmacenado { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long TamanoBytes { get; set; }
    public DateTime FechaCreacion { get; set; }
}
