using System.Net;
using System.Text;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Configuracion;
using Capacitaciones.Application.Dtos.Notifications;
using Capacitaciones.Infrastructure.Services;

namespace Capacitaciones.Tests;

public class MailSenderHttpClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastPath = request.RequestUri!.AbsolutePath;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }

    private static (MailSenderHttpClient, StubHandler) Build(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://mail_sender:8000/") };
        return (new MailSenderHttpClient(http), handler);
    }

    private static SendMailRequest Req() => new()
    {
        Template = "certificado_participante", Subject = "s", Recipients = new() { "x@dos.com.ec" }
    };

    [Fact]
    public async Task SendMail_StatusSent_DevuelveEnviado()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{\"status\":\"sent\"}");
        Assert.Equal(MailSendResult.Enviado, await client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendMail_StatusOmitido_DevuelveOmitido()
    {
        var (client, _) = Build(HttpStatusCode.OK, "{\"status\":\"omitido\"}");
        Assert.Equal(MailSendResult.Omitido, await client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendMail_CuerpoVacio_DevuelveEnviado()
    {
        var (client, _) = Build(HttpStatusCode.OK, "");
        Assert.Equal(MailSendResult.Enviado, await client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendMail_502_Lanza()
    {
        var (client, _) = Build(HttpStatusCode.BadGateway, "{\"detail\":\"x\"}");
        await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMailAsync(Req(), CancellationToken.None));
    }

    [Fact]
    public async Task SendTest_EnviaContratoCamelCaseYLeeResultado()
    {
        var (client, handler) = Build(HttpStatusCode.OK, "{\"ok\":false,\"error\":\"535 Authentication failed\"}");

        var result = await client.SendTestAsync(new SendTestMailRequest
        {
            Recipient = "admin@dos.com.ec",
            Smtp = new SmtpInternoDto
            {
                Host = "smtp.office365.com", Port = 587, Password = "p", UseTls = true,
                From = "capacitaciones@dos.com.ec", FromName = "CapacitaDOS"
            }
        }, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal("535 Authentication failed", result.Error);
        Assert.Equal("/send-test", handler.LastPath);
        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("admin@dos.com.ec", doc.RootElement.GetProperty("recipient").GetString());
        Assert.Equal("capacitaciones@dos.com.ec", doc.RootElement.GetProperty("smtp").GetProperty("from").GetString());
        Assert.True(doc.RootElement.GetProperty("smtp").GetProperty("useTls").GetBoolean());
    }
}
