namespace Capacitaciones.Application.Dtos.Inscritos;

/// <summary>Fila de la lista admin de inscritos. No incluye la firma.</summary>
public class InscritoDto
{
    public Guid Id { get; set; }
    public Guid CapacitacionId { get; set; }
    public string CapacitacionCodigo { get; set; } = string.Empty;
    public string CapacitacionTema { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;
    public Guid AreaId { get; set; }
    public string AreaNombre { get; set; } = string.Empty;

    /// <summary>Correo completo (con <c>@dos.com.ec</c>).</summary>
    public string Email { get; set; } = string.Empty;

    public DateTime FechaInscripcion { get; set; }
    public bool TieneFirma { get; set; }
}

/// <summary>Detalle para el modal de edición / ver firma.</summary>
public class InscritoDetalleDto : InscritoDto
{
    /// <summary>Solo la parte local del correo (sin <c>@dos.com.ec</c>), para el campo editable.</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>Firma base64 (data URL) o null.</summary>
    public string? Firma { get; set; }
}

/// <summary>Payload de <c>PUT /api/inscritos/{id}</c>.</summary>
public class EditarInscritoDto
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identificacion { get; set; } = string.Empty;
    public Guid AreaId { get; set; }

    /// <summary>Parte local del correo (sin <c>@</c>).</summary>
    public string EmailUsuario { get; set; } = string.Empty;

    /// <summary>Firma nueva (data URL). Null o vacía = conservar la actual.</summary>
    public string? Firma { get; set; }
}
