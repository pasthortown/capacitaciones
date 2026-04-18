using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests de integración del módulo Repositorio. Usan una factory con InMemory EF y
/// un <see cref="InMemoryResourceStorageAdapter"/> que evita tocar el filesystem real.
/// </summary>
public class RecursosEndpointTests : IClassFixture<RecursosEndpointTests.RecursosWebAppFactory>
{
    private readonly RecursosWebAppFactory _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RecursosEndpointTests(RecursosWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_Autenticado_Retorna201ConDto()
    {
        using var factory = new RecursosWebAppFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await UploadSampleAsync(client, "doc.pdf", "Guía rápida", "Resumen del curso");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RecursoDetailDto>(JsonOpts);
        Assert.NotNull(dto);
        Assert.Equal("Guía rápida", dto!.NombreOriginal);
        Assert.Equal("pdf", dto.Extension);
        Assert.True(dto.Activo);
    }

    [Fact]
    public async Task Upload_SinAuth_Retorna401()
    {
        using var factory = new RecursosWebAppFactory();
        var client = factory.CreateClient();

        var response = await UploadSampleAsync(client, "doc.pdf", "Nombre", "Descripción");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ExtensionBloqueada_Retorna400()
    {
        using var factory = new RecursosWebAppFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await UploadSampleAsync(client, "malware.exe", "Malware", "Prueba");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("EXTENSION_PROHIBIDA", body);
    }

    [Fact]
    public async Task Get_List_Put_Delete_FlowCompleto()
    {
        using var factory = new RecursosWebAppFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        // Crear
        var create = await UploadSampleAsync(client, "archivo.txt", "Archivo", "descripción inicial");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var creado = await create.Content.ReadFromJsonAsync<RecursoDetailDto>(JsonOpts);

        // Listar (activos)
        var listActivos = await client.GetFromJsonAsync<List<RecursoListDto>>("/api/recursos", JsonOpts);
        Assert.NotNull(listActivos);
        Assert.Contains(listActivos!, r => r.Id == creado!.Id);

        // GET by id
        var get = await client.GetAsync($"/api/recursos/{creado!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        // Editar metadata (sólo nombre + descripción, sin archivo)
        using (var putForm = new MultipartFormDataContent())
        {
            putForm.Add(new StringContent("renombrado.txt"), "nombreOriginal");
            putForm.Add(new StringContent("nueva descripcion"), "descripcion");

            var put = await client.PutAsync($"/api/recursos/{creado.Id}", putForm);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var editado = await put.Content.ReadFromJsonAsync<RecursoDetailDto>(JsonOpts);
            Assert.Equal("renombrado.txt", editado!.NombreOriginal);
            Assert.NotNull(editado.FechaActualizacion);
            // Sin archivo nuevo, NombreAlmacenado no cambia.
            Assert.Equal(creado.NombreAlmacenado, editado.NombreAlmacenado);
        }

        // Editar con reemplazo de archivo
        using (var putForm = new MultipartFormDataContent())
        {
            putForm.Add(new StringContent("renombrado.txt"), "nombreOriginal");
            putForm.Add(new StringContent("nueva descripcion"), "descripcion");
            var nuevoBytes = Encoding.UTF8.GetBytes("contenido reemplazado");
            var fc = new ByteArrayContent(nuevoBytes);
            fc.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            putForm.Add(fc, "archivo", "otro.txt");

            var put = await client.PutAsync($"/api/recursos/{creado.Id}", putForm);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            var editado = await put.Content.ReadFromJsonAsync<RecursoDetailDto>(JsonOpts);
            Assert.NotEqual(creado.NombreAlmacenado, editado!.NombreAlmacenado);
            Assert.Equal(nuevoBytes.Length, editado.TamanoBytes);
        }

        // Delete
        var del = await client.DeleteAsync($"/api/recursos/{creado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Listado (activos) ya no lo incluye
        var listActivos2 = await client.GetFromJsonAsync<List<RecursoListDto>>("/api/recursos", JsonOpts);
        Assert.DoesNotContain(listActivos2!, r => r.Id == creado.Id);

        // Con includeInactive sí aparece, pero Activo=false
        var listTodos = await client.GetFromJsonAsync<List<RecursoListDto>>("/api/recursos?includeInactive=true", JsonOpts);
        var inactivo = listTodos!.FirstOrDefault(r => r.Id == creado.Id);
        Assert.NotNull(inactivo);
        Assert.False(inactivo!.Activo);
    }

    [Fact]
    public async Task DescargaPublica_SinAuth_IncluyeContentDispositionYContenido()
    {
        using var factory = new RecursosWebAppFactory();
        var adminClient = await factory.CreateAuthenticatedClientAsync();

        var create = await UploadSampleAsync(adminClient, "notas.txt", "Nótas.txt", "Prueba de descarga", content: "contenido-test");
        var dto = await create.Content.ReadFromJsonAsync<RecursoDetailDto>(JsonOpts);

        // Cliente sin auth para la descarga pública.
        var publicClient = factory.CreateClient();
        var response = await publicClient.GetAsync($"/api/publico/recursos/{dto!.Id}/descargar");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Header Content-Disposition con filename y filename* (UTF-8).
        Assert.True(response.Content.Headers.TryGetValues("Content-Disposition", out var headerValues));
        var header = string.Join(";", headerValues!);
        Assert.Contains("attachment", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filename", header);
        Assert.Contains("filename*=UTF-8''", header);
        // El nombre codificado RFC 5987 debe contener la forma escapada de "Nótas.txt".
        Assert.Contains("N%C3%B3tas.txt", header);

        // El contenido fluye a través de la descarga.
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("contenido-test", body);
    }

    [Fact]
    public async Task DescargaPublica_Inexistente_Retorna404()
    {
        using var factory = new RecursosWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/publico/recursos/{Guid.NewGuid()}/descargar");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Link_RetornaUrlRelativaPublica()
    {
        using var factory = new RecursosWebAppFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var creado = await (await UploadSampleAsync(client, "a.pdf", "A", "desc"))
            .Content.ReadFromJsonAsync<RecursoDetailDto>(JsonOpts);

        var response = await client.PostAsync($"/api/recursos/{creado!.Id}/link", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var link = await response.Content.ReadFromJsonAsync<LinkDescargaRecursoDto>(JsonOpts);
        Assert.Equal(creado.Id, link!.RecursoId);
        Assert.Equal($"/api/publico/recursos/{creado.Id}/descargar", link.Url);
        Assert.Equal("A", link.NombreOriginal);
    }

    // ----- Helpers -----

    private static async Task<HttpResponseMessage> UploadSampleAsync(
        HttpClient client,
        string filename,
        string nombre,
        string descripcion,
        string content = "contenido")
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(filename));
        form.Add(fileContent, "archivo", filename);
        form.Add(new StringContent(nombre), "nombre");
        form.Add(new StringContent(descripcion), "descripcion");

        return await client.PostAsync("/api/recursos", form);
    }

    private static string GuessContentType(string filename)
    {
        var ext = System.IO.Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "pdf" => "application/pdf",
            "txt" => "text/plain",
            "exe" => "application/octet-stream",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Factory con EF InMemory + admin sembrado + <see cref="InMemoryResourceStorageAdapter"/>.
    /// Evita depender del directorio <c>/repository</c> que no existe en CI.
    /// </summary>
    public class RecursosWebAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = "RecursosTestDb_" + Guid.NewGuid();

        public const string SeededAdminEmail = "recursos.admin@dos.com.ec";
        public const string SeededAdminPassword = "ChangeMe!2026";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Reemplaza el DbContext por InMemory.
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(opt =>
                {
                    opt.UseInMemoryDatabase(_databaseName);
                    opt.ConfigureWarnings(w => w.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });

                // Reemplaza el IResourceStorage real por el adaptador en memoria.
                var storageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IResourceStorage));
                if (storageDescriptor is not null) services.Remove(storageDescriptor);
                services.AddSingleton<IResourceStorage, InMemoryResourceStorageAdapter>();

                using var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();

                if (!db.AdminUsers.Any(u => u.Email == SeededAdminEmail))
                {
                    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                    db.AdminUsers.Add(new Capacitaciones.Domain.Entities.AdminUser
                    {
                        Id = Guid.NewGuid(),
                        Email = SeededAdminEmail,
                        PasswordHash = hasher.Hash(SeededAdminPassword),
                        Nombres = "Recursos Admin",
                        Activo = true,
                        FechaCreacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                        FechaActualizacion = null,
                        UltimoLogin = null
                    });
                    db.SaveChanges();
                }
            });
        }

        public async Task<HttpClient> CreateAuthenticatedClientAsync()
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new
            {
                email = SeededAdminEmail,
                password = SeededAdminPassword
            });
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<LoginPayload>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                payload?.Token ?? throw new InvalidOperationException("Login no devolvió token."));
            return client;
        }

        private sealed class LoginPayload
        {
            public string Token { get; set; } = string.Empty;
        }
    }

    /// <summary>Adaptador en memoria registrado en los tests vía DI.</summary>
    internal sealed class InMemoryResourceStorageAdapter : IResourceStorage
    {
        private readonly Dictionary<string, byte[]> _saved = new();
        private readonly object _lock = new();

        public async Task SaveAsync(Stream content, string storedName, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            lock (_lock) _saved[storedName] = ms.ToArray();
        }

        public bool Exists(string storedName)
        {
            lock (_lock) return _saved.ContainsKey(storedName);
        }

        public Task DeleteAsync(string storedName, CancellationToken ct)
        {
            lock (_lock) _saved.Remove(storedName);
            return Task.CompletedTask;
        }

        public Stream OpenRead(string storedName)
        {
            byte[] bytes;
            lock (_lock)
            {
                if (!_saved.TryGetValue(storedName, out var found))
                    throw new FileNotFoundException(storedName);
                bytes = found;
            }
            return new MemoryStream(bytes, writable: false);
        }

        public string GetAbsolutePath(string storedName) => $"/in-memory/{storedName}";
    }
}
