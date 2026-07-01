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
/// El costo se detalla en <see cref="Items"/> (monto total = suma de ítems); la base de devengo es
/// <see cref="ValorAsumidoEmpresa"/>. El valor se devenga a lo largo de <see cref="MesesADevengar"/>
/// (0 = no aplica), anclado en <see cref="FechaIngreso"/>, de forma proporcional mensual o escalonada
/// según la clasificación del <see cref="Tipo"/>. El <see cref="Estado"/> rige cómo se calcula lo
/// adeudado; al pasar a <c>Cobrado</c>/<c>Anulado</c> se congela el pendiente en
/// <see cref="MontoCongelado"/> con <see cref="FechaCorte"/>.
///
/// <see cref="Activo"/> es baja lógica (visibilidad), independiente del estado de negocio.
/// </summary>
public class Convenio
{
    public Guid Id { get; set; }

    /// <summary>Número de registro secuencial del anexo (formateado como <c>GIC-EC-REG-###</c>).
    /// Nullable solo para convenios legados aún sin numerar (backfill en migración).</summary>
    public int? NumeroRegistro { get; set; }

    public string CedulaColaborador { get; set; } = string.Empty;
    public string NombreColaborador { get; set; } = string.Empty;
    public string? OrigenColaborador { get; set; }
    /// <summary>Cargo del colaborador al crear/editar (snapshot, para impresión).</summary>
    public string? CargoColaborador { get; set; }
    /// <summary>Área del colaborador = Departamento (snapshot, para impresión).</summary>
    public string? AreaColaborador { get; set; }
    /// <summary>Empresa del colaborador (snapshot pre-llenado desde la fuente; texto libre).</summary>
    public string? EmpresaColaborador { get; set; }
    /// <summary>Género del colaborador (snapshot desde la fuente; para indicadores del dashboard).</summary>
    public string? GeneroColaborador { get; set; }
    /// <summary>Centro de costos (captura manual en el anexo; no existe en las fuentes).</summary>
    public string? CentroCostos { get; set; }
    /// <summary>Jefe inmediato (captura manual en el anexo).</summary>
    public string? JefeInmediato { get; set; }
    /// <summary>Relación laboral / tipo de contrato (captura manual en el anexo).</summary>
    public string? RelacionLaboral { get; set; }
    /// <summary>Fecha de ingreso del colaborador. <b>Ancla del cálculo de devengación</b> (distinta
    /// de <see cref="FechaCreacion"/>, que es la fecha de registro en el sistema).</summary>
    public DateTime? FechaIngreso { get; set; }
    /// <summary>Fecha en que se firma el documento del anexo.</summary>
    public DateTime? FechaFirma { get; set; }

    public string? Descripcion { get; set; }
    /// <summary>Tipo de evento formativo (Curso o capacitación, Certificación, Examen de
    /// certificación, Diplomado, Programa especializado, Material de estudio). Determina la
    /// clasificación, plazo y modalidad de reintegro.</summary>
    public string? Tipo { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }

    /// <summary>Convenio previo del que este es parte o continuación (auto-referencia, opcional).
    /// Permite encadenar rutas de capacitación/certificación.</summary>
    public Guid? ConvenioReferenciaId { get; set; }
    /// <summary>Navegación al convenio referenciado (para exponer su código y nombre en el DTO).</summary>
    public Convenio? ConvenioReferencia { get; set; }

    public DateTime? FechaInicioCurso { get; set; }
    /// <summary>Fecha de fin / aprobación del curso.</summary>
    public DateTime? FechaFinCurso { get; set; }
    /// <summary>Duración del evento en horas.</summary>
    public decimal? Horas { get; set; }
    /// <summary>Resultado (Aprobado, En curso, Pendiente, No aprobado).</summary>
    public string? Resultado { get; set; }
    /// <summary>Switch que el usuario marca cuando ha cargado el convenio firmado.</summary>
    public bool ConvenioFirmado { get; set; }

    /// <summary>Quién solicitó el convenio (texto libre).</summary>
    public string? SolicitadoPor { get; set; }
    /// <summary>Quién autorizó el convenio (texto libre).</summary>
    public string? AutorizadoPor { get; set; }

    /// <summary>Fecha del convenio. Fallback de ancla de devengo cuando no hay <see cref="FechaIngreso"/>.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Valor asumido por la empresa. <b>Base del cálculo de devengación</b> (se establece
    /// bajo el monto total = suma de ítems de costo).</summary>
    public decimal ValorAsumidoEmpresa { get; set; }

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
