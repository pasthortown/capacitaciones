namespace Capacitaciones.Application.Dtos.Certificados;

/// <summary>
/// Resumen del flujo "Generar y Enviar todos los certificados". Se devuelve
/// 200 OK incluso si algunos asistentes fallaron — el UI presenta los detalles.
/// Reusa los contadores de generación (<see cref="GeneracionLoteResultadoDto"/>)
/// y agrega los del envío de correo.
/// </summary>
public class GeneracionEnvioLoteResultadoDto
{
    public int Total { get; set; }
    public int Emitidos { get; set; }
    public int NoElegibles { get; set; }
    public List<GeneracionLoteNoElegibleDto> NoElegiblesDetalle { get; set; } = new();
    public List<GeneracionLoteErrorDto> Errores { get; set; } = new();

    /// <summary>Cantidad de correos que se entregaron al servicio mail_sender con éxito.</summary>
    public int Enviados { get; set; }

    /// <summary>Detalle de envíos que fallaron (PDF ausente, sin email, error SMTP, etc.).</summary>
    public List<EnvioCertificadoErrorDto> ErroresEnvio { get; set; } = new();
}

public class EnvioCertificadoErrorDto
{
    public Guid AsistenteId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}
