using System.Security.Cryptography;
using Capacitaciones.Infrastructure.Security;

namespace Capacitaciones.Tests;

public class AesGcmSecretProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Protect_Unprotect_IdaYVuelta()
    {
        var p = new AesGcmSecretProtector(NewKey());
        var cipher = p.Protect("S3cr3t*ñ");
        Assert.NotEqual("S3cr3t*ñ", cipher);
        Assert.Equal("S3cr3t*ñ", p.TryUnprotect(cipher));
    }

    [Fact]
    public void Protect_MismoTexto_GeneraCifradosDistintos()
    {
        var p = new AesGcmSecretProtector(NewKey());
        Assert.NotEqual(p.Protect("abc"), p.Protect("abc"));
    }

    [Fact]
    public void TryUnprotect_ConOtraLlave_DevuelveNull()
    {
        var cipher = new AesGcmSecretProtector(NewKey()).Protect("abc");
        Assert.Null(new AesGcmSecretProtector(NewKey()).TryUnprotect(cipher));
    }

    [Fact]
    public void TryUnprotect_TextoBasura_DevuelveNull()
    {
        var p = new AesGcmSecretProtector(NewKey());
        Assert.Null(p.TryUnprotect("no-es-base64!!"));
        Assert.Null(p.TryUnprotect(Convert.ToBase64String(new byte[5])));
    }

    [Fact]
    public void SinLlave_NoConfigurado_ProtectLanza_TryUnprotectNull()
    {
        var p = new AesGcmSecretProtector(null);
        Assert.False(p.IsConfigured);
        Assert.Throws<InvalidOperationException>(() => p.Protect("abc"));
        Assert.Null(p.TryUnprotect("abc"));
    }

    [Fact]
    public void LlaveDeLongitudIncorrecta_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            new AesGcmSecretProtector(Convert.ToBase64String(new byte[16])));
    }
}
