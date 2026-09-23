using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.UseCases.Configuracion;
using Capacitaciones.Infrastructure.Persistence;
using Capacitaciones.Infrastructure.Security;
using Capacitaciones.Tests.Fakes;
using Capacitaciones.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Capacitaciones.Tests;

public class NotificacionesUseCasesTests
{
    [Fact]
    public void Seed_CreaLosNueveAvisosActivos()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("SeedNotificaciones_" + Guid.NewGuid())
            .Options;
        using var db = new AppDbContext(options);
        db.Database.EnsureCreated();

        var filas = db.ConfiguracionNotificaciones.OrderBy(n => n.Plantilla).ToList();

        Assert.Equal(9, filas.Count);
        Assert.All(filas, f => Assert.True(f.Activo));
        Assert.All(filas, f => Assert.Null(f.AsuntoPersonalizado));
        Assert.Equal(
            NotificacionesCatalogo.Todas.Select(d => d.Plantilla).OrderBy(p => p),
            filas.Select(f => f.Plantilla));
    }

    [Fact]
    public async Task Listar_DevuelveCatalogoConVariablesYAsuntoOriginal()
    {
        var uc = new ListarNotificacionesUseCase(new InMemoryConfiguracionNotificacionRepository());
        var lista = await uc.ExecuteAsync();

        Assert.Equal(9, lista.Count);
        Assert.Equal("invitacion_inscripcion", lista[0].Plantilla);
        // Misma clave que EnviarInvitacionInscripcionUseCase envía en Parameters.
        Assert.Contains("tipoActividad", lista[0].Variables);
        var cert = lista.Single(n => n.Plantilla == "certificado_participante");
        Assert.Equal("Tu certificado: {tema}", cert.AsuntoActual);
        Assert.Contains("tema", cert.Variables);
        Assert.Contains("asunto_original", cert.Variables);
    }

    [Fact]
    public async Task Actualizar_CambiaSoloLasEnviadasYRecortaAsunto()
    {
        var repo = new InMemoryConfiguracionNotificacionRepository();
        var uc = new ActualizarNotificacionesUseCase(repo);

        await uc.ExecuteAsync(new List<UpdateNotificacionDto>
        {
            new() { Plantilla = "encuesta_satisfaccion", Activo = false, AsuntoPersonalizado = "  " },
            new() { Plantilla = "certificado_participante", Activo = true, AsuntoPersonalizado = "  [DOS] {{ tema }} " }
        }, "admin@dos.com.ec");

        var encuesta = repo.Items.Single(i => i.Plantilla == "encuesta_satisfaccion");
        Assert.False(encuesta.Activo);
        Assert.Null(encuesta.AsuntoPersonalizado);
        Assert.Equal("admin@dos.com.ec", encuesta.ActualizadoPor);

        var cert = repo.Items.Single(i => i.Plantilla == "certificado_participante");
        Assert.Equal("[DOS] {{ tema }}", cert.AsuntoPersonalizado);

        Assert.True(repo.Items.Single(i => i.Plantilla == "responsable_firma").Activo);
        Assert.Null(repo.Items.Single(i => i.Plantilla == "responsable_firma").ActualizadoPor);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Actualizar_PlantillaDesconocida_LanzaSinGuardar()
    {
        var repo = new InMemoryConfiguracionNotificacionRepository();
        var uc = new ActualizarNotificacionesUseCase(repo);

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(
            new List<UpdateNotificacionDto> { new() { Plantilla = "no_existe", Activo = true } }, "a@dos.com.ec"));

        Assert.Equal("PLANTILLA_DESCONOCIDA", ex.Codigo);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Actualizar_AsuntoMuyLargo_Lanza()
    {
        var uc = new ActualizarNotificacionesUseCase(new InMemoryConfiguracionNotificacionRepository());

        var ex = await Assert.ThrowsAsync<ConfiguracionCorreoException>(() => uc.ExecuteAsync(
            new List<UpdateNotificacionDto>
            {
                new() { Plantilla = "encuesta_satisfaccion", Activo = true, AsuntoPersonalizado = new string('x', 501) }
            }, "a@dos.com.ec"));

        Assert.Equal("ASUNTO_MUY_LARGO", ex.Codigo);
    }

    [Fact]
    public async Task Interna_SinConfiguracion_DevuelveReglasSinSmtp()
    {
        var notif = new InMemoryConfiguracionNotificacionRepository();
        notif.Items.Single(i => i.Plantilla == "encuesta_satisfaccion").Activo = false;
        var uc = new ObtenerConfiguracionCorreoInternaUseCase(
            new InMemoryConfiguracionCorreoRepository(), notif,
            new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

        var dto = await uc.ExecuteAsync();

        Assert.False(dto.Configurado);
        Assert.Null(dto.Smtp);
        Assert.Equal(9, dto.Notificaciones.Count);
        Assert.False(dto.Notificaciones["encuesta_satisfaccion"].Activo);
    }

    [Fact]
    public async Task Interna_Configurada_DescifraPasswordYParseaCopias()
    {
        var protector = new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var repo = new InMemoryConfiguracionCorreoRepository
        {
            Current = new ConfiguracionCorreo
            {
                SmtpHost = "smtp.office365.com", SmtpPort = 587, SmtpUser = null,
                SmtpPasswordCifrada = protector.Protect("Clave123*"), UsarTls = true,
                RemitenteCorreo = "capacitaciones@dos.com.ec", RemitenteNombre = "CapacitaDOS",
                CcGlobal = "a@dos.com.ec, b@dos.com.ec", BccGlobal = null,
                ActualizadoPor = "x", ActualizadoEn = DateTime.UtcNow
            }
        };
        var uc = new ObtenerConfiguracionCorreoInternaUseCase(repo, new InMemoryConfiguracionNotificacionRepository(), protector);

        var dto = await uc.ExecuteAsync();

        Assert.True(dto.Configurado);
        Assert.False(dto.PasswordInvalida);
        Assert.Equal("Clave123*", dto.Smtp!.Password);
        Assert.Equal("capacitaciones@dos.com.ec", dto.Smtp.From);
        Assert.Equal(new[] { "a@dos.com.ec", "b@dos.com.ec" }, dto.CcGlobal);
        Assert.Empty(dto.BccGlobal);
    }

    [Fact]
    public async Task Interna_PasswordIlegible_MarcaPasswordInvalida()
    {
        var repo = new InMemoryConfiguracionCorreoRepository
        {
            Current = new ConfiguracionCorreo
            {
                SmtpHost = "h", SmtpPort = 587, SmtpPasswordCifrada = "basura",
                RemitenteCorreo = "r@dos.com.ec", ActualizadoPor = "x", ActualizadoEn = DateTime.UtcNow
            }
        };
        var uc = new ObtenerConfiguracionCorreoInternaUseCase(
            repo, new InMemoryConfiguracionNotificacionRepository(),
            new AesGcmSecretProtector(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

        var dto = await uc.ExecuteAsync();

        Assert.True(dto.PasswordInvalida);
        Assert.Null(dto.Smtp!.Password);
    }
}
