using System.Text.Json;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>
/// Caso de uso: enviar un correo de prueba con los datos del formulario (aunque no estén
/// guardados) al admin que lo pide. Si no se escribe contraseña, usa la guardada (solo si
/// servidor, puerto y usuario no cambiaron).
/// </summary>
public class EnviarCorreoPruebaUseCase
{
    private readonly IConfiguracionCorreoRepository _repo;
    private readonly ISecretProtector _protector;
    private readonly IMailSenderClient _mail;

    public EnviarCorreoPruebaUseCase(
        IConfiguracionCorreoRepository repo, ISecretProtector protector, IMailSenderClient mail)
    {
        _repo = repo;
        _protector = protector;
        _mail = mail;
    }

    public async Task<CorreoPruebaResultadoDto> ExecuteAsync(
        UpdateConfiguracionCorreoDto input, string adminEmail, CancellationToken ct = default)
    {
        if (input is null) throw new ConfiguracionCorreoException("INVALID_INPUT", "Payload requerido.");

        var errores = ConfiguracionCorreoValidator.Validar(input);
        if (errores.Count > 0)
        {
            throw new ConfiguracionCorreoException("VALIDACION", "Revisa los campos marcados.", errores);
        }

        string? password;
        if (input.QuitarPassword)
        {
            password = null;
        }
        else if (!string.IsNullOrEmpty(input.Password))
        {
            password = input.Password;
        }
        else
        {
            var existente = await _repo.GetAsync(ct);
            ConfiguracionCorreoValidator.ExigirPasswordSiCambiaServidor(input, existente);
            var guardada = existente?.SmtpPasswordCifrada;
            password = string.IsNullOrEmpty(guardada) ? null : _protector.TryUnprotect(guardada);
        }

        var request = new SendTestMailRequest
        {
            Recipient = adminEmail,
            Smtp = new SmtpInternoDto
            {
                Host = input.SmtpHost.Trim(),
                Port = input.SmtpPort,
                User = string.IsNullOrWhiteSpace(input.SmtpUser) ? null : input.SmtpUser.Trim(),
                Password = password,
                UseTls = input.UsarTls,
                From = input.RemitenteCorreo.Trim(),
                FromName = string.IsNullOrWhiteSpace(input.RemitenteNombre) ? null : input.RemitenteNombre.Trim()
            }
        };

        try
        {
            var result = await _mail.SendTestAsync(request, ct);
            return result.Ok
                ? new CorreoPruebaResultadoDto { Ok = true, Mensaje = $"Correo de prueba enviado a {adminEmail}." }
                : new CorreoPruebaResultadoDto { Ok = false, Mensaje = result.Error ?? "Error desconocido del servidor SMTP." };
        }
        catch (HttpRequestException ex)
        {
            return new CorreoPruebaResultadoDto
            {
                Ok = false,
                Mensaje = $"No se pudo contactar al servicio de correo: {ex.Message}"
            };
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout del HttpClient (no cancelación del caller): el HttpClient de MailSenderHttpClient
            // lanza TaskCanceledException, no HttpRequestException, cuando expira su Timeout configurado.
            return new CorreoPruebaResultadoDto
            {
                Ok = false,
                Mensaje = "El servicio de correo no respondió a tiempo. Revisa el servidor y el puerto SMTP."
            };
        }
        catch (JsonException)
        {
            // Respuesta 2xx pero no JSON válido: no es un error de red, es un contrato roto.
            return new CorreoPruebaResultadoDto
            {
                Ok = false,
                Mensaje = "Respuesta inválida del servicio de correo."
            };
        }
    }
}
