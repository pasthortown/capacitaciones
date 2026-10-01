using Capacitaciones.Application.Dtos.Inscripcion;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Inscripcion;

/// <summary>
/// Autocompletado por cédula de la página pública de inscripción. Busca solo en el registro
/// propio (<c>PersonaInscrita</c>) — no consulta DOS/ControlTareas ni Externos.
///
/// Nunca devuelve la firma: solo <see cref="PersonaInscritaLookupDto.TieneFirma"/>. El correo va
/// sin el sufijo <c>@dos.com.ec</c> (el formulario captura solo la parte local) y el área solo si
/// sigue activa.
/// </summary>
public class BuscarPersonaInscritaUseCase
{
    private const string EmailDomain = "@dos.com.ec";

    private readonly IPersonaInscritaRepository _personas;
    private readonly IAreaRepository _areas;

    public BuscarPersonaInscritaUseCase(IPersonaInscritaRepository personas, IAreaRepository areas)
    {
        _personas = personas;
        _areas = areas;
    }

    public async Task<PersonaInscritaLookupDto?> ExecuteAsync(string identificacion, CancellationToken ct = default)
    {
        var id = (identificacion ?? string.Empty).Trim();
        if (id.Length == 0) return null;

        var persona = await _personas.GetByIdentificacionAsync(id, ct);
        if (persona is null) return null;

        Guid? areaId = null;
        if (persona.AreaId is Guid guardada)
        {
            var area = await _areas.GetByIdAsync(guardada, ct);
            if (area is not null && area.Activo) areaId = area.Id;
        }

        var email = persona.EmailUsuario ?? string.Empty;
        if (email.EndsWith(EmailDomain, StringComparison.OrdinalIgnoreCase))
        {
            email = email[..^EmailDomain.Length];
        }

        return new PersonaInscritaLookupDto
        {
            Nombres = persona.Nombres,
            Apellidos = persona.Apellidos,
            AreaId = areaId,
            EmailUsuario = email,
            TieneFirma = !string.IsNullOrWhiteSpace(persona.Firma)
        };
    }
}
