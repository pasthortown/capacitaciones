using System.Security.Claims;
using Capacitaciones.Application.Dtos.Capacitaciones;
using Capacitaciones.Application.UseCases.Calificaciones;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.Capacitador;
using Capacitaciones.Application.UseCases.Certificados;
using Capacitaciones.Application.UseCases.Inscripcion;
using Capacitaciones.Application.UseCases.Notifications;
using Capacitaciones.Application.UseCases.PaseLista;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capacitaciones.Api.Controllers;

/// <summary>
/// CRUD de capacitaciones. Todos los endpoints requieren la policy "Admin"
/// (Fase 2 configuró JWT + policy).
/// </summary>
[ApiController]
[Authorize(Policy = "Admin")]
[Route("api/capacitaciones")]
public class CapacitacionesController : ControllerBase
{
    // Firmas en base64 pueden ser grandes (>100KB) y una capacitación puede traer múltiples
    // responsables. 10 MB es un margen amplio sin exponer a DoS por cuerpos gigantes.
    private const int MaxRequestBodyBytes = 10_000_000;

    /// <summary>
    /// Fase 9: tamaño máximo permitido para el upload del logo (2 MB + holgura para el
    /// boundary del multipart). Debe quedar coherente con <see cref="LogoCapacitacionPolicy.MaxBytes"/>.
    /// </summary>
    private const long MaxLogoUploadBytes = 3_000_000;

    private readonly ListarCapacitacionesUseCase _listar;
    private readonly ObtenerCapacitacionUseCase _obtener;
    private readonly CrearCapacitacionUseCase _crear;
    private readonly EditarCapacitacionUseCase _editar;
    private readonly EliminarCapacitacionUseCase _eliminar;
    private readonly GenerarLinkCapacitadorUseCase _generarLinkCapacitador;
    private readonly GenerarLinkInscripcionUseCase _generarLinkInscripcion;
    private readonly GenerarCertificadosCapacitacionUseCase _generarCertificados;
    private readonly SubirLogoCapacitacionUseCase _subirLogo;
    private readonly EliminarLogoCapacitacionUseCase _eliminarLogo;
    private readonly GenerarLinkPaseListaUseCase _generarLinkPaseLista;
    private readonly GenerarLinkCalificacionesUseCase _generarLinkCalificaciones;
    private readonly NotificarResumenCapacitacionUseCase _notificarResumen;
    private readonly NotificarLinksCapacitadorUseCase _notificarCapacitador;
    private readonly EnviarInvitacionInscripcionUseCase _enviarInvitacion;
    private readonly ILogger<CapacitacionesController> _logger;

    public CapacitacionesController(
        ListarCapacitacionesUseCase listar,
        ObtenerCapacitacionUseCase obtener,
        CrearCapacitacionUseCase crear,
        EditarCapacitacionUseCase editar,
        EliminarCapacitacionUseCase eliminar,
        GenerarLinkCapacitadorUseCase generarLinkCapacitador,
        GenerarLinkInscripcionUseCase generarLinkInscripcion,
        GenerarCertificadosCapacitacionUseCase generarCertificados,
        SubirLogoCapacitacionUseCase subirLogo,
        EliminarLogoCapacitacionUseCase eliminarLogo,
        GenerarLinkPaseListaUseCase generarLinkPaseLista,
        GenerarLinkCalificacionesUseCase generarLinkCalificaciones,
        NotificarResumenCapacitacionUseCase notificarResumen,
        NotificarLinksCapacitadorUseCase notificarCapacitador,
        EnviarInvitacionInscripcionUseCase enviarInvitacion,
        ILogger<CapacitacionesController> logger)
    {
        _listar = listar;
        _obtener = obtener;
        _crear = crear;
        _editar = editar;
        _eliminar = eliminar;
        _generarLinkCapacitador = generarLinkCapacitador;
        _generarLinkInscripcion = generarLinkInscripcion;
        _generarCertificados = generarCertificados;
        _subirLogo = subirLogo;
        _eliminarLogo = eliminarLogo;
        _generarLinkPaseLista = generarLinkPaseLista;
        _generarLinkCalificaciones = generarLinkCalificaciones;
        _notificarResumen = notificarResumen;
        _notificarCapacitador = notificarCapacitador;
        _enviarInvitacion = enviarInvitacion;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? estado = null,
        CancellationToken ct = default)
    {
        var items = await _listar.ExecuteAsync(includeInactive, estado, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}", Name = "Capacitaciones_GetById")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _obtener.ExecuteAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCapacitacionDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _crear.ExecuteAsync(input, ct);
            await NotificarAdminAsync(dto.Id, isCreate: true, ct);
            await NotificarCapacitadorSilencioAsync(dto.Id, ct);
            var location = Url.Action("GetById", new { id = dto.Id }) ?? string.Empty;
            return Created(location, dto);
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCapacitacionDto input,
        CancellationToken ct)
    {
        try
        {
            var dto = await _editar.ExecuteAsync(id, input, ct);
            await NotificarAdminAsync(dto.Id, isCreate: false, ct);
            return Ok(dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Endpoint manual para reenviar al capacitador los correos con los enlaces +
    /// QR de "cargar descripción/firma" y "pase de lista". Lo dispara el botón
    /// "Enviar correos para capacitador" del dashboard. También se ejecuta en
    /// automático cuando se crea la capacitación (vía
    /// <see cref="NotificarCapacitadorSilencioAsync"/>); este endpoint queda
    /// para reenvíos posteriores o cuando el email del capacitador se agregue
    /// más tarde.
    /// </summary>
    /// <summary>
    /// Genera y envía al admin autenticado el correo de "invitación a
    /// inscribirse". El admin lo recibe en su buzón y lo reenvía a quienes
    /// quiera invitar — el email lleva tono de invitación, datos del evento,
    /// link público y QR de inscripción.
    /// </summary>
    [HttpPost("{id:guid}/enviar-invitacion-inscripcion")]
    public async Task<IActionResult> EnviarInvitacionInscripcion(Guid id, CancellationToken ct)
    {
        var adminEmail = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
        try
        {
            var resultado = await _enviarInvitacion.ExecuteAsync(id, adminEmail ?? string.Empty, ct);
            return Ok(resultado);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
        catch (Exception ex) when (IsMailSenderFailure(ex, ct))
        {
            _logger.LogWarning(ex, "mail_sender falló al enviar la invitación de inscripción de la capacitación {CapacitacionId}.", id);
            return new ObjectResult(new { error = "MAIL_SENDER_NO_DISPONIBLE", message = "No se pudo enviar el correo: el servicio de correos no respondió a tiempo." })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
    }

    [HttpPost("{id:guid}/notificar-capacitador")]
    public async Task<IActionResult> NotificarCapacitador(Guid id, CancellationToken ct)
    {
        try
        {
            var resultado = await _notificarCapacitador.ExecuteAsync(id, ct);
            return Ok(resultado);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
        catch (Exception ex) when (IsMailSenderFailure(ex, ct))
        {
            // Falla del servicio mail_sender (red, 5xx, timeout, o SMTP egress
            // bloqueado). Mapeamos a 502 para que el front muestre un error
            // claro y el admin pueda reintentar.
            _logger.LogWarning(ex, "mail_sender falló al notificar al capacitador de la capacitación {CapacitacionId}.", id);
            return new ObjectResult(new { error = "MAIL_SENDER_NO_DISPONIBLE", message = "No se pudo enviar el correo: el servicio de correos no respondió a tiempo." })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
    }

    /// <summary>
    /// Dispara el correo "resumen del evento" al admin que está autenticado.
    /// Es no-bloqueante en el sentido de que jamás revierte el create/update:
    /// captura cualquier excepción del cliente HTTP a <c>mail_sender</c> y
    /// la registra como warning. La operación principal ya se persistió.
    /// </summary>
    private async Task NotificarAdminAsync(Guid capacitacionId, bool isCreate, CancellationToken ct)
    {
        var adminEmail = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            return;
        }

        try
        {
            await _notificarResumen.ExecuteAsync(capacitacionId, adminEmail, isCreate, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo enviar el correo de resumen ({Operacion}) de la capacitación {CapacitacionId} al admin {AdminEmail}.",
                isCreate ? "creación" : "edición",
                capacitacionId,
                adminEmail);
        }
    }

    /// <summary>
    /// Versión silenciosa del notificar-capacitador: la dispara el flujo de
    /// creación. Si la capacitación todavía no tiene email del capacitador,
    /// simplemente no envía nada (no es error: el admin podrá reenviar luego
    /// con el endpoint manual). Cualquier otro fallo se loggea como warning
    /// y nunca afecta al 201.
    /// </summary>
    private async Task NotificarCapacitadorSilencioAsync(Guid capacitacionId, CancellationToken ct)
    {
        try
        {
            await _notificarCapacitador.ExecuteAsync(capacitacionId, ct);
        }
        catch (CapacitacionServiceException ex) when (ex.Codigo == "EMAIL_CAPACITADOR_REQUERIDO")
        {
            // Capacitador sin email aún: no hay correo que mandar — sale en silencio.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudieron enviar los correos al capacitador de la capacitación {CapacitacionId}.",
                capacitacionId);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _eliminar.ExecuteAsync(id, ct);
            return NoContent();
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Fase 4: genera un link firmado (JWT role=Capacitador) para entregarle al capacitador.
    /// El body de la respuesta incluye la URL relativa y la fecha de expiración.
    /// Cada invocación emite un token nuevo que convive con los anteriores hasta expirar
    /// (no hay lista negra — ver nota en <c>GenerarLinkCapacitadorUseCase</c>).
    /// </summary>
    [HttpPost("{id:guid}/link-capacitador")]
    public async Task<IActionResult> GenerarLinkCapacitador(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLinkCapacitador.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Fase 5: genera un link firmado (JWT role=Inscripcion) para el formulario público de inscripción.
    /// Cada invocación emite un token NUEVO que convive con los anteriores hasta expirar (no hay lista negra).
    /// </summary>
    [HttpPost("{id:guid}/link-inscripcion")]
    public async Task<IActionResult> GenerarLinkInscripcion(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLinkInscripcion.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Fase 10: genera un link firmado (JWT role=PaseLista) para el flujo de pase de lista.
    /// Es un token independiente del de capacitador (Fase 4): un link filtrado solo habilita
    /// pase de lista, no permite editar descripción/firma ni viceversa.
    /// </summary>
    [HttpPost("{id:guid}/link-pase-lista")]
    public async Task<IActionResult> GenerarLinkPaseLista(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLinkPaseLista.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Fase 11: genera un link firmado (JWT role=Calificaciones) para el flujo de registro
    /// de calificaciones. Es un token independiente de los otros dos links del capacitador.
    /// Solo válido si la capacitación es <c>TipoCertificacion == Aprobacion</c>; en caso
    /// contrario responde 409 <c>CALIFICACIONES_NO_APLICA</c>.
    /// </summary>
    [HttpPost("{id:guid}/link-calificaciones")]
    public async Task<IActionResult> GenerarLinkCalificaciones(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _generarLinkCalificaciones.ExecuteAsync(id, ct);
            return Ok(dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Fase 6: dispara la emisión masiva de certificados para todos los asistentes de la
    /// capacitación. El endpoint es idempotente — puede invocarse múltiples veces para
    /// regenerar. Devuelve <c>200 OK</c> con un resumen incluso si algunos fallan; el UI
    /// muestra la lista de errores y permite reintentar por asistente.
    /// </summary>
    [HttpPost("{id:guid}/certificados/generar")]
    public async Task<IActionResult> GenerarCertificados(Guid id, CancellationToken ct)
    {
        try
        {
            var resultado = await _generarCertificados.ExecuteAsync(id, ct);
            return Ok(resultado);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CertificadoNoDisponibleException ex)
        {
            return new ObjectResult(new { error = ex.Codigo, message = ex.Message })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
    }

    /// <summary>
    /// Fase 9 — carga (o reemplazo) del logo de la capacitación. Acepta png/jpg/jpeg/webp/svg
    /// hasta 2 MB. Si ya había un logo, lo reemplaza (el archivo anterior se borra físicamente).
    /// </summary>
    [HttpPost("{id:guid}/logo")]
    [RequestSizeLimit(MaxLogoUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxLogoUploadBytes)]
    public async Task<IActionResult> SubirLogo(Guid id, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return ToProblem(new CapacitacionServiceException("LOGO_VACIO", "El archivo está vacío o no se recibió."));
        }

        try
        {
            await using var stream = archivo.OpenReadStream();
            var dto = await _subirLogo.ExecuteAsync(
                id,
                stream,
                archivo.FileName,
                archivo.ContentType ?? string.Empty,
                archivo.Length,
                ct);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Fase 9 — elimina el logo de la capacitación (archivo físico + columnas).
    /// Idempotente: si la capacitación no tenía logo, devuelve 204 igualmente.
    /// </summary>
    [HttpDelete("{id:guid}/logo")]
    public async Task<IActionResult> EliminarLogo(Guid id, CancellationToken ct)
    {
        try
        {
            await _eliminarLogo.ExecuteAsync(id, ct);
            return NoContent();
        }
        catch (CapacitacionNotFoundException)
        {
            return NotFound();
        }
        catch (CapacitacionServiceException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Decide si una excepción capturada en los endpoints de notificación
    /// proviene del cliente HTTP a <c>mail_sender</c> y por tanto debe
    /// mapearse a 502, o si fue una cancelación real del request por parte
    /// del cliente (token <paramref name="userCt"/> cancelado), en cuyo caso
    /// se deja propagar.
    /// </summary>
    private static bool IsMailSenderFailure(Exception ex, CancellationToken userCt)
    {
        // Si el cliente abortó la petición HTTP entrante, la respuesta ya no
        // importa — dejamos que el framework lo maneje.
        if (userCt.IsCancellationRequested) return false;

        return ex is HttpRequestException
            || ex is TaskCanceledException
            || ex is OperationCanceledException;
    }

    private static ObjectResult ToProblem(CapacitacionServiceException ex)
    {
        var status = ex.Codigo switch
        {
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "ASISTENTE_NOT_FOUND" => StatusCodes.Status404NotFound,
            "CAPACITACION_INACTIVA" => StatusCodes.Status409Conflict,
            "RESPONSABLE_DUPLICADO" => StatusCodes.Status409Conflict,
            "LOGO_DEMASIADO_GRANDE" => StatusCodes.Status413PayloadTooLarge,
            "LOGO_VACIO" => StatusCodes.Status400BadRequest,
            "LOGO_NOMBRE_REQUERIDO" => StatusCodes.Status400BadRequest,
            "LOGO_EXTENSION_INVALIDA" => StatusCodes.Status400BadRequest,
            "LOGO_CONTENT_TYPE_INVALIDO" => StatusCodes.Status400BadRequest,
            "LOGO_CONTENT_TYPE_INCOHERENTE" => StatusCodes.Status400BadRequest,
            "ESTADO_ASISTENCIA_INVALIDO" => StatusCodes.Status400BadRequest,
            "EMAIL_CAPACITADOR_REQUERIDO" => StatusCodes.Status422UnprocessableEntity,
            "ADMIN_EMAIL_REQUERIDO" => StatusCodes.Status401Unauthorized,
            "CALIFICACIONES_NO_APLICA" => StatusCodes.Status409Conflict,
            "ASISTENTE_NO_PRESENTE" => StatusCodes.Status409Conflict,
            "CALIFICACION_FUERA_DE_RANGO" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };
        return new ObjectResult(new { error = ex.Codigo, message = ex.Message }) { StatusCode = status };
    }
}
