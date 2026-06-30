namespace Capacitaciones.Application.Dtos.Convenios;

/// <summary>
/// Payload de alta/edición de un convenio. <c>Cedula</c> identifica al colaborador (se resuelve
/// su nombre/origen en el backend). <c>Fecha</c> en <c>yyyy-MM-dd</c>. <c>MesesADevengar</c> ∈
/// {0,12,24,36} (0 = no aplica devengo). <c>Activo</c> opcional: <c>true</c> reactiva.
/// </summary>
public class ConvenioRequest
{
    public string? Cedula { get; set; }

    // --- Snapshot extendido del colaborador (capturado en el anexo) ---
    /// <summary>Centro de costos (manual; no existe en las fuentes).</summary>
    public string? CentroCostos { get; set; }
    /// <summary>Jefe inmediato (manual).</summary>
    public string? JefeInmediato { get; set; }
    /// <summary>Relación laboral / tipo de contrato (manual).</summary>
    public string? RelacionLaboral { get; set; }
    /// <summary>Fecha de ingreso del colaborador (yyyy-MM-dd). <b>Requerida</b>: ancla el devengo.</summary>
    public string? FechaIngreso { get; set; }
    /// <summary>Fecha de firma del documento (yyyy-MM-dd).</summary>
    public string? FechaFirma { get; set; }

    public string? Titulo { get; set; }
    public string? Descripcion { get; set; }
    /// <summary>Tipo de evento (Curso o capacitación, Certificación, Examen de certificación,
    /// Diplomado, Programa especializado, Material de estudio).</summary>
    public string? Tipo { get; set; }
    public string? TipoCurso { get; set; }
    public string? NombreCurso { get; set; }
    public string? Marca { get; set; }

    // --- Detalle del evento ---
    public string? FechaInicioCurso { get; set; }
    public string? FechaFinCurso { get; set; }
    public decimal? Horas { get; set; }
    /// <summary>Aprobado | En curso | Pendiente | No aprobado.</summary>
    public string? Resultado { get; set; }
    /// <summary>El usuario lo marca cuando ha cargado el convenio firmado.</summary>
    public bool ConvenioFirmado { get; set; }

    public string? SolicitadoPor { get; set; }
    public string? AutorizadoPor { get; set; }
    public string? Fecha { get; set; }

    /// <summary>Valor asumido por la empresa: base del cálculo de devengación.</summary>
    public decimal ValorAsumidoEmpresa { get; set; }
    public int MesesADevengar { get; set; }
    /// <summary>"Vigente" | "Devengado" | "Cobrado" | "Anulado". Default Vigente.</summary>
    public string? Estado { get; set; }
    public List<ConvenioItemRequest>? Items { get; set; }
    public bool? Activo { get; set; }
}
