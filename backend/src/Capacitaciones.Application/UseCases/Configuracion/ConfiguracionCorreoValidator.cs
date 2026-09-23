using System.Net.Mail;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Configuracion;

/// <summary>Validación y normalización de <see cref="UpdateConfiguracionCorreoDto"/>.</summary>
public static class ConfiguracionCorreoValidator
{
    public const int MaxCopias = 20;

    /// <summary>Devuelve los errores por campo (nombre de propiedad del DTO). Vacío = válido.</summary>
    public static Dictionary<string, string> Validar(UpdateConfiguracionCorreoDto input)
    {
        var errores = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(input.SmtpHost))
            errores[nameof(input.SmtpHost)] = "El servidor SMTP es obligatorio.";
        if (input.SmtpPort < 1 || input.SmtpPort > 65535)
            errores[nameof(input.SmtpPort)] = "El puerto debe estar entre 1 y 65535.";
        if (!EsEmail(input.RemitenteCorreo))
            errores[nameof(input.RemitenteCorreo)] = "Ingresa un correo de remitente válido.";
        if (!string.IsNullOrWhiteSpace(input.SmtpUser) && input.SmtpUser.Trim().Length > 255)
            errores[nameof(input.SmtpUser)] = "El usuario no puede superar 255 caracteres.";
        if (!string.IsNullOrWhiteSpace(input.RemitenteNombre) && input.RemitenteNombre.Trim().Length > 255)
            errores[nameof(input.RemitenteNombre)] = "El nombre no puede superar 255 caracteres.";

        ValidarLista(input.CcGlobal, nameof(input.CcGlobal), errores);
        ValidarLista(input.BccGlobal, nameof(input.BccGlobal), errores);
        return errores;
    }

    /// <summary>
    /// Impide reutilizar la contraseña guardada contra otro servidor o usuario: si el formulario
    /// no trae contraseña (ni pide quitarla), hay una guardada y cambió el host, el puerto o el
    /// usuario efectivo, lanza VALIDACION con error en Password. Así la contraseña guardada no se
    /// puede enviar a un host elegido por quien edita el formulario.
    /// </summary>
    public static void ExigirPasswordSiCambiaServidor(UpdateConfiguracionCorreoDto input, ConfiguracionCorreo? guardada)
    {
        if (!string.IsNullOrEmpty(input.Password) || input.QuitarPassword) return;
        if (guardada is null || string.IsNullOrEmpty(guardada.SmtpPasswordCifrada)) return;
        if (MismoServidorYUsuario(input, guardada)) return;

        throw new ConfiguracionCorreoException("VALIDACION", "Revisa los campos marcados.", new Dictionary<string, string>
        {
            [nameof(input.Password)] = "Vuelve a ingresar la contraseña al cambiar de servidor o usuario."
        });
    }

    /// <summary>
    /// True si host (sin mayúsculas/espacios), puerto y usuario efectivo (usuario SMTP, o el
    /// remitente si no hay usuario) coinciden con los guardados.
    /// </summary>
    public static bool MismoServidorYUsuario(UpdateConfiguracionCorreoDto input, ConfiguracionCorreo guardada)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        return comparer.Equals(input.SmtpHost?.Trim() ?? "", guardada.SmtpHost?.Trim() ?? "")
            && input.SmtpPort == guardada.SmtpPort
            && comparer.Equals(
                UsuarioEfectivo(input.SmtpUser, input.RemitenteCorreo),
                UsuarioEfectivo(guardada.SmtpUser, guardada.RemitenteCorreo));
    }

    private static string UsuarioEfectivo(string? usuario, string? remitente) =>
        string.IsNullOrWhiteSpace(usuario) ? remitente?.Trim() ?? "" : usuario.Trim();

    /// <summary>Separa por coma o punto y coma, recorta y descarta vacíos.</summary>
    public static List<string> ParsearLista(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? new List<string>()
            : valor.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

    /// <summary>Lista normalizada como texto "a@x, b@x"; null si queda vacía.</summary>
    public static string? Normalizar(string? valor)
    {
        var items = ParsearLista(valor);
        return items.Count == 0 ? null : string.Join(", ", items);
    }

    public static bool EsEmail(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return false;
        var trimmed = valor.Trim();
        return MailAddress.TryCreate(trimmed, out var addr) && addr.Address == trimmed;
    }

    private static void ValidarLista(string? valor, string campo, Dictionary<string, string> errores)
    {
        var items = ParsearLista(valor);
        if (items.Count > MaxCopias)
        {
            errores[campo] = $"Máximo {MaxCopias} direcciones.";
            return;
        }
        var invalido = items.FirstOrDefault(i => !EsEmail(i));
        if (invalido is not null)
        {
            errores[campo] = $"'{invalido}' no es un correo válido.";
        }
        else if (Normalizar(valor)?.Length > 1000)
        {
            errores[campo] = "La lista no puede superar 1000 caracteres.";
        }
    }
}
