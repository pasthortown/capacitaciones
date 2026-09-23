using System.Security.Cryptography;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Tests.Fakes;

namespace Capacitaciones.Tests;

/// <summary>
/// Cubre cómo <see cref="EnviarCorreoPruebaUseCase"/> traduce las distintas formas en que
/// IMailSenderClient puede fallar (timeout, respuesta no-JSON, error HTTP) a un resultado
/// Ok=false en vez de dejar que la excepción escale a un 500 — spec 4.4: el endpoint
/// POST /api/configuracion/correo/prueba siempre responde 200 {ok, mensaje}.
/// </summary>
public class EnviarCorreoPruebaUseCaseTests
{
    private const string Admin = "admin@dos.com.ec";

    private static AesGcmSecretProtector NewProtector() =>
        new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    private static UpdateConfiguracionCorreoDto ValidInput() => new()
    {
        SmtpHost = "smtp.office365.com",
        SmtpPort = 587,
        SmtpUser = null,
        Password = "Clave123*",
        UsarTls = true,
        RemitenteCorreo = "capacitaciones@dos.com.ec",
        RemitenteNombre = "CapacitaDOS",
        CcGlobal = null,
        BccGlobal = null
    };

    /// <summary>Stub mínimo que lanza la excepción configurada desde SendTestAsync.</summary>
    private sealed class ThrowingMailSenderClient : IMailSenderClient
    {
        private readonly Func<CancellationToken, Exception> _factory;

        public ThrowingMailSenderClient(Func<CancellationToken, Exception> factory)
        {
            _factory = factory;
        }

        public Task<MailSendResult> SendMailAsync(SendMailRequest request, CancellationToken ct) =>
            throw new NotSupportedException("No usado en estos tests.");

        public Task<MailTestResult> SendTestAsync(SendTestMailRequest request, CancellationToken ct) =>
            throw _factory(ct);
    }

    [Fact]
    public async Task Timeout_TaskCanceledSinCancelacionDelCaller_DevuelveOkFalse()
    {
        var mail = new ThrowingMailSenderClient(_ => new TaskCanceledException("El HttpClient venció su Timeout."));
        var uc = new EnviarCorreoPruebaUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector(), mail);

        var resultado = await uc.ExecuteAsync(ValidInput(), Admin, CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.Contains("no respondió a tiempo", resultado.Mensaje);
    }

    [Fact]
    public async Task RespuestaNoJson_JsonException_DevuelveOkFalse()
    {
        var mail = new ThrowingMailSenderClient(_ => new JsonException("'<' es un carácter JSON inválido."));
        var uc = new EnviarCorreoPruebaUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector(), mail);

        var resultado = await uc.ExecuteAsync(ValidInput(), Admin, CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.Contains("Respuesta inválida", resultado.Mensaje);
    }

    [Fact]
    public async Task ErrorHttp_HttpRequestException_DevuelveOkFalse()
    {
        var mail = new ThrowingMailSenderClient(_ => new HttpRequestException("Connection refused"));
        var uc = new EnviarCorreoPruebaUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector(), mail);

        var resultado = await uc.ExecuteAsync(ValidInput(), Admin, CancellationToken.None);

        Assert.False(resultado.Ok);
        Assert.Contains("No se pudo contactar al servicio de correo", resultado.Mensaje);
    }

    [Fact]
    public async Task CancelacionDelCaller_PropagaOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        // El stub simula lo que hace HttpClient cuando el CancellationToken del caller ya está
        // cancelado: lanza TaskCanceledException con ese mismo token cancelado.
        var mail = new ThrowingMailSenderClient(ct => new TaskCanceledException("Cancelado por el caller.", null, ct));
        var uc = new EnviarCorreoPruebaUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector(), mail);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => uc.ExecuteAsync(ValidInput(), Admin, cts.Token));
    }

    private static async Task<(InMemoryConfiguracionCorreoRepository Repo, AesGcmSecretProtector Protector)> RepoConPasswordGuardada()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var protector = NewProtector();
        await new ActualizarConfiguracionCorreoUseCase(repo, protector).ExecuteAsync(ValidInput(), Admin);
        return (repo, protector);
    }

    [Fact]
    public async Task SinPasswordMismoServidorYUsuario_UsaLaGuardada()
    {
        var (repo, protector) = await RepoConPasswordGuardada();
        var mail = new FakeMailSenderClient();
        var uc = new EnviarCorreoPruebaUseCase(repo, protector, mail);
        var input = ValidInput();
        input.Password = null;
        input.SmtpHost = "SMTP.OFFICE365.COM";

        var resultado = await uc.ExecuteAsync(input, Admin);

        Assert.True(resultado.Ok);
        Assert.Equal("Clave123*", mail.LastTest!.Smtp.Password);
    }

    [Theory]
    [InlineData("atacante.example.com", 587, null)]
    [InlineData("smtp.office365.com", 2525, null)]
    [InlineData("smtp.office365.com", 587, "otro@dos.com.ec")]
    public async Task SinPasswordCambiaServidorOUsuario_Lanza400EnPasswordSinEnviar(
        string host, int port, string? user)
    {
        var (repo, protector) = await RepoConPasswordGuardada();
        var mail = new FakeMailSenderClient();
        var uc = new EnviarCorreoPruebaUseCase(repo, protector, mail);
        var input = ValidInput();
        input.Password = "";
        input.SmtpHost = host;
        input.SmtpPort = port;
        input.SmtpUser = user;

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(input, Admin));

        Assert.Equal("VALIDACION", ex.Codigo);
        Assert.True(ex.Errores.ContainsKey("Password"));
        Assert.Null(mail.LastTest);
    }

    [Fact]
    public async Task CambiaServidorConPasswordNueva_EnviaConLaNueva()
    {
        var (repo, protector) = await RepoConPasswordGuardada();
        var mail = new FakeMailSenderClient();
        var uc = new EnviarCorreoPruebaUseCase(repo, protector, mail);
        var input = ValidInput();
        input.SmtpHost = "otro.servidor.com";
        input.Password = "Nueva456*";

        var resultado = await uc.ExecuteAsync(input, Admin);

        Assert.True(resultado.Ok);
        Assert.Equal("otro.servidor.com", mail.LastTest!.Smtp.Host);
        Assert.Equal("Nueva456*", mail.LastTest.Smtp.Password);
    }
}
