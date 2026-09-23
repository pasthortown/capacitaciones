using System.Security.Cryptography;
using System.Text;
using Capacitaciones.Application.Ports;

namespace Capacitaciones.Infrastructure.Security;

/// <summary>
/// <see cref="ISecretProtector"/> con AES-256-GCM. Formato del texto cifrado:
/// base64( nonce[12] ‖ tag[16] ‖ ciphertext ). La llave (32 bytes en base64) llega de
/// la variable <c>CORREO_ENCRYPTION_KEY</c>.
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[]? _key;

    public AesGcmSecretProtector(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key)) return;
        var key = Convert.FromBase64String(base64Key.Trim());
        if (key.Length != 32)
        {
            throw new ArgumentException("CORREO_ENCRYPTION_KEY debe ser de 32 bytes codificados en base64.");
        }
        _key = key;
    }

    public bool IsConfigured => _key is not null;

    public string Protect(string plain)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("CORREO_ENCRYPTION_KEY no está configurada.");
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string? TryUnprotect(string cipher)
    {
        if (_key is null || string.IsNullOrWhiteSpace(cipher)) return null;
        try
        {
            var data = Convert.FromBase64String(cipher);
            if (data.Length < NonceSize + TagSize) return null;

            var nonce = data.AsSpan(0, NonceSize);
            var tag = data.AsSpan(NonceSize, TagSize);
            var encrypted = data.AsSpan(NonceSize + TagSize);
            var plain = new byte[encrypted.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, encrypted, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
