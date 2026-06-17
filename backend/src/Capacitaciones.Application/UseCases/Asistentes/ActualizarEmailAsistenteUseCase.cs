using Capacitaciones.Application.Dtos.Asistentes;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Application.UseCases.PaseLista;

namespace Capacitaciones.Application.UseCases.Asistentes;

/// <summary>
/// Admin: corrige el email de un asistente. Algunos correos capturados en la inscripción
/// pública pueden venir mal escritos y provocar fallos de envío del certificado; editarlos
/// permite reintentar el envío. A diferencia de la inscripción (que fuerza <c>@dos.com.ec</c>),
/// aquí se acepta un email completo arbitrario — el asistente podría tener un correo externo.
/// </summary>
public class ActualizarEmailAsistenteUseCase
{
    private readonly ICapacitacionRepository _capacitaciones;
    private readonly IAsistenteRepository _asistentes;

    public ActualizarEmailAsistenteUseCase(
        ICapacitacionRepository capacitaciones,
        IAsistenteRepository asistentes)
    {
        _capacitaciones = capacitaciones;
        _asistentes = asistentes;
    }

    public async Task<EmailAsistenteResponseDto> ExecuteAsync(
        Guid capacitacionId,
        Guid asistenteId,
        string? email,
        CancellationToken ct = default)
    {
        var capacitacion = await _capacitaciones.GetByIdWithResponsablesAsync(capacitacionId, ct)
            ?? throw new CapacitacionNotFoundException(capacitacionId);

        var asistente = await _asistentes.GetByIdAsync(asistenteId, ct)
            ?? throw new AsistenteNotFoundException(asistenteId);

        if (asistente.CapacitacionId != capacitacion.Id)
        {
            throw new AsistenteNotFoundException(asistenteId);
        }

        var normalizado = (email ?? string.Empty).Trim();
        if (!EsEmailValido(normalizado))
        {
            throw new CapacitacionServiceException(
                "EMAIL_INVALIDO",
                "El correo no tiene un formato válido.");
        }

        asistente.EmailUsuario = normalizado;
        await _asistentes.UpdateAsync(asistente, ct);

        return new EmailAsistenteResponseDto
        {
            Id = asistente.Id,
            Email = asistente.EmailUsuario
        };
    }

    private static bool EsEmailValido(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 255) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
