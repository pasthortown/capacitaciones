using System.Security.Cryptography;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Tests.Fakes;

namespace Capacitaciones.Tests;

public class ConfiguracionCorreoUseCasesTests
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
        CcGlobal = "a@dos.com.ec; b@dos.com.ec",
        BccGlobal = null
    };

    [Fact]
    public async Task Obtener_SinFila_DevuelveNoConfiguradoConDefaults()
    {
        var uc = new ObtenerConfiguracionCorreoUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector());
        var dto = await uc.ExecuteAsync();

        Assert.False(dto.Configurado);
        Assert.Equal(587, dto.SmtpPort);
        Assert.True(dto.UsarTls);
        Assert.False(dto.TienePassword);
    }

    [Fact]
    public async Task Actualizar_Valido_GuardaCifradoYNormalizaListas()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var protector = NewProtector();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, protector);

        var dto = await uc.ExecuteAsync(ValidInput(), Admin);

        Assert.True(dto.Configurado);
        Assert.True(dto.TienePassword);
        Assert.Equal("a@dos.com.ec, b@dos.com.ec", dto.CcGlobal);
        Assert.NotNull(repo.Current);
        Assert.NotEqual("Clave123*", repo.Current!.SmtpPasswordCifrada);
        Assert.Equal("Clave123*", protector.TryUnprotect(repo.Current.SmtpPasswordCifrada!));
        Assert.Equal(Admin, repo.Current.ActualizadoPor);
    }

    [Fact]
    public async Task Actualizar_SinPassword_ConservaLaGuardada()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var protector = NewProtector();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, protector);
        await uc.ExecuteAsync(ValidInput(), Admin);
        var cifradaAntes = repo.Current!.SmtpPasswordCifrada;

        var input = ValidInput();
        input.Password = "";
        input.RemitenteNombre = "Otro nombre";
        await uc.ExecuteAsync(input, Admin);

        Assert.Equal(cifradaAntes, repo.Current!.SmtpPasswordCifrada);
        Assert.Equal("Otro nombre", repo.Current.RemitenteNombre);
    }

    [Fact]
    public async Task Actualizar_QuitarPassword_LaBorra()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        await uc.ExecuteAsync(ValidInput(), Admin);

        var input = ValidInput();
        input.Password = null;
        input.QuitarPassword = true;
        var dto = await uc.ExecuteAsync(input, Admin);

        Assert.Null(repo.Current!.SmtpPasswordCifrada);
        Assert.False(dto.TienePassword);
    }

    [Theory]
    [InlineData("SmtpHost", "", 587, "capacitaciones@dos.com.ec", null)]
    [InlineData("SmtpPort", "smtp.x.com", 0, "capacitaciones@dos.com.ec", null)]
    [InlineData("SmtpPort", "smtp.x.com", 70000, "capacitaciones@dos.com.ec", null)]
    [InlineData("RemitenteCorreo", "smtp.x.com", 587, "no-es-correo", null)]
    [InlineData("CcGlobal", "smtp.x.com", 587, "capacitaciones@dos.com.ec", "ok@dos.com.ec, malo")]
    public async Task Actualizar_Invalido_LanzaConErrorDelCampo(
        string campo, string host, int port, string remitente, string? cc)
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        var input = ValidInput();
        input.SmtpHost = host;
        input.SmtpPort = port;
        input.RemitenteCorreo = remitente;
        input.CcGlobal = cc;

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(input, Admin));

        Assert.Equal("VALIDACION", ex.Codigo);
        Assert.True(ex.Errores.ContainsKey(campo));
        Assert.Null(repo.Current);
    }

    [Fact]
    public async Task Actualizar_MasDe20Copias_Lanza()
    {
        var uc = new ActualizarConfiguracionCorreoUseCase(new InMemoryConfiguracionCorreoRepository(), NewProtector());
        var input = ValidInput();
        input.BccGlobal = string.Join(",", Enumerable.Range(1, 21).Select(i => $"u{i}@dos.com.ec"));

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(input, Admin));
        Assert.True(ex.Errores.ContainsKey("BccGlobal"));
    }

    [Fact]
    public async Task Actualizar_PasswordNuevaSinLlave_LanzaEncryptionKeyMissing()
    {
        var uc = new ActualizarConfiguracionCorreoUseCase(
            new InMemoryConfiguracionCorreoRepository(), new AesGcmSecretProtector(null));

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(ValidInput(), Admin));
        Assert.Equal("ENCRYPTION_KEY_MISSING", ex.Codigo);
    }

    [Fact]
    public async Task Obtener_PasswordCifradaConOtraLlave_MarcaPasswordInvalida()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        await new ActualizarConfiguracionCorreoUseCase(repo, NewProtector()).ExecuteAsync(ValidInput(), Admin);

        var dto = await new ObtenerConfiguracionCorreoUseCase(repo, NewProtector()).ExecuteAsync();

        Assert.True(dto.TienePassword);
        Assert.True(dto.PasswordInvalida);
    }

    [Fact]
    public async Task Actualizar_SinPasswordMismoServidorYUsuario_ReutilizaLaGuardada()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        await uc.ExecuteAsync(ValidInput(), Admin);
        var cifradaAntes = repo.Current!.SmtpPasswordCifrada;

        var input = ValidInput();
        input.Password = "";
        input.SmtpHost = "  SMTP.Office365.com ";
        input.SmtpUser = "Capacitaciones@DOS.com.ec"; // igual al remitente usado como usuario efectivo
        await uc.ExecuteAsync(input, Admin);

        Assert.Equal(cifradaAntes, repo.Current!.SmtpPasswordCifrada);
    }

    [Theory]
    [InlineData("otro.servidor.com", 587, null)]
    [InlineData("smtp.office365.com", 25, null)]
    [InlineData("smtp.office365.com", 587, "otro@dos.com.ec")]
    public async Task Actualizar_SinPasswordCambiaServidorOUsuario_Lanza400EnPassword(
        string host, int port, string? user)
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        await uc.ExecuteAsync(ValidInput(), Admin);
        var cifradaAntes = repo.Current!.SmtpPasswordCifrada;

        var input = ValidInput();
        input.Password = null;
        input.SmtpHost = host;
        input.SmtpPort = port;
        input.SmtpUser = user;

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(input, Admin));

        Assert.Equal("VALIDACION", ex.Codigo);
        Assert.Equal("Vuelve a ingresar la contraseña al cambiar de servidor o usuario.", ex.Errores["Password"]);
        Assert.Equal("smtp.office365.com", repo.Current!.SmtpHost);
        Assert.Equal(cifradaAntes, repo.Current.SmtpPasswordCifrada);
    }

    [Fact]
    public async Task Actualizar_CambiaServidorConPasswordNueva_Guarda()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var protector = NewProtector();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, protector);
        await uc.ExecuteAsync(ValidInput(), Admin);

        var input = ValidInput();
        input.SmtpHost = "otro.servidor.com";
        input.Password = "Nueva456*";
        await uc.ExecuteAsync(input, Admin);

        Assert.Equal("otro.servidor.com", repo.Current!.SmtpHost);
        Assert.Equal("Nueva456*", protector.TryUnprotect(repo.Current.SmtpPasswordCifrada!));
    }

    [Fact]
    public async Task Actualizar_CambiaServidorYQuitaPassword_Guarda()
    {
        var repo = new InMemoryConfiguracionCorreoRepository();
        var uc = new ActualizarConfiguracionCorreoUseCase(repo, NewProtector());
        await uc.ExecuteAsync(ValidInput(), Admin);

        var input = ValidInput();
        input.SmtpHost = "relay.interno";
        input.Password = null;
        input.QuitarPassword = true;
        await uc.ExecuteAsync(input, Admin);

        Assert.Equal("relay.interno", repo.Current!.SmtpHost);
        Assert.Null(repo.Current.SmtpPasswordCifrada);
    }
}
