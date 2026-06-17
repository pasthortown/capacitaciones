using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Capacitaciones;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios de los casos de uso de logo de capacitación (Fase 9).
/// Usan repo + storage en memoria — sin EF ni HTTP.
/// </summary>
public class LogoCapacitacionUseCasesTests
{
    [Fact]
    public async Task Subir_HappyPath_PersisteArchivoYActualizaEntidad()
    {
        var (repo, storage, capacitacion) = NewFixture();
        var useCase = new SubirLogoCapacitacionUseCase(repo, storage);

        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // Firma PNG (no se valida, solo documenta)
        using var ms = new MemoryStream(bytes);

        var dto = await useCase.ExecuteAsync(
            capacitacion.Id,
            ms,
            "logo-marca.png",
            "image/png",
            bytes.Length);

        Assert.EndsWith(".png", dto.LogoPath);
        Assert.Equal("image/png", dto.LogoContentType);
        Assert.StartsWith("/imagenes/", dto.LogoUrl);
        Assert.True(storage.Saved.ContainsKey(dto.LogoPath));
        Assert.Equal(dto.LogoPath, capacitacion.LogoPath);
        Assert.Equal("image/png", capacitacion.LogoContentType);
        Assert.NotNull(capacitacion.FechaActualizacion);
    }

    [Fact]
    public async Task Subir_ExtensionNoPermitida_Rechaza()
    {
        var (repo, storage, capacitacion) = NewFixture();
        var useCase = new SubirLogoCapacitacionUseCase(repo, storage);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, ms, "logo.gif", "image/gif", 3));

        Assert.Equal("LOGO_EXTENSION_INVALIDA", ex.Codigo);
        Assert.Empty(storage.Saved);
    }

    [Fact]
    public async Task Subir_ContentTypeIncoherente_Rechaza()
    {
        var (repo, storage, capacitacion) = NewFixture();
        var useCase = new SubirLogoCapacitacionUseCase(repo, storage);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, ms, "logo.png", "image/jpeg", 3));

        Assert.Equal("LOGO_CONTENT_TYPE_INCOHERENTE", ex.Codigo);
    }

    [Fact]
    public async Task Subir_ArchivoMayorA2MB_Rechaza()
    {
        var (repo, storage, capacitacion) = NewFixture();
        var useCase = new SubirLogoCapacitacionUseCase(repo, storage);

        using var ms = new MemoryStream(new byte[] { 1 });
        var ex = await Assert.ThrowsAsync<CapacitacionServiceException>(() =>
            useCase.ExecuteAsync(capacitacion.Id, ms, "logo.png", "image/png", LogoCapacitacionPolicy.MaxBytes + 1));

        Assert.Equal("LOGO_DEMASIADO_GRANDE", ex.Codigo);
    }

    [Fact]
    public async Task Subir_ReemplazoBorraLogoAnterior()
    {
        var (repo, storage, capacitacion) = NewFixture();
        // Simulamos que la capacitación ya tenía un logo previo.
        capacitacion.LogoPath = "logo-anterior.png";
        capacitacion.LogoContentType = "image/png";
        storage.Saved["logo-anterior.png"] = new byte[] { 0 };

        var useCase = new SubirLogoCapacitacionUseCase(repo, storage);
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });

        var dto = await useCase.ExecuteAsync(capacitacion.Id, ms, "nuevo.webp", "image/webp", 3);

        Assert.False(storage.Saved.ContainsKey("logo-anterior.png"), "Debe borrarse el archivo anterior");
        Assert.True(storage.Saved.ContainsKey(dto.LogoPath));
        Assert.Equal(dto.LogoPath, capacitacion.LogoPath);
    }

    [Fact]
    public async Task Subir_CapacitacionNoExiste_LanzaNotFound()
    {
        var (repo, storage, _) = NewFixture();
        var useCase = new SubirLogoCapacitacionUseCase(repo, storage);
        using var ms = new MemoryStream(new byte[] { 1 });

        await Assert.ThrowsAsync<CapacitacionNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid(), ms, "x.png", "image/png", 1));
    }

    [Fact]
    public async Task Eliminar_LimpiaColumnasYBorraArchivo()
    {
        var (repo, storage, capacitacion) = NewFixture();
        capacitacion.LogoPath = "abcd.png";
        capacitacion.LogoContentType = "image/png";
        storage.Saved["abcd.png"] = new byte[] { 1, 2 };

        var useCase = new EliminarLogoCapacitacionUseCase(repo, storage);
        await useCase.ExecuteAsync(capacitacion.Id);

        Assert.Null(capacitacion.LogoPath);
        Assert.Null(capacitacion.LogoContentType);
        Assert.False(storage.Saved.ContainsKey("abcd.png"));
    }

    [Fact]
    public async Task Eliminar_SinLogo_EsIdempotente()
    {
        var (repo, storage, capacitacion) = NewFixture();
        var useCase = new EliminarLogoCapacitacionUseCase(repo, storage);

        await useCase.ExecuteAsync(capacitacion.Id);

        Assert.Null(capacitacion.LogoPath);
    }

    // ----- Helpers -----

    private static (InMemoryCapacitacionRepository repo, InMemoryLogoStorage storage, Capacitacion capacitacion) NewFixture()
    {
        var capacitacion = new Capacitacion
        {
            Id = Guid.NewGuid(),
            Codigo = "CAP-PC-REG-001",
            Tema = "Test",
            Capacitador = "Tester",
            TipoCertificacion = TipoCertificacion.Participacion,
            FechaHoraInicio = DateTime.UtcNow.AddDays(1),
            DuracionMinutos = 60,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        var repo = new InMemoryCapacitacionRepository();
        repo.Stored[capacitacion.Id] = capacitacion;
        return (repo, new InMemoryLogoStorage(), capacitacion);
    }

    /// <summary>Repo mínimo: solo los métodos usados por los use cases de logo.</summary>
    private sealed class InMemoryCapacitacionRepository : ICapacitacionRepository
    {
        public Task<string?> GetLatestFirmaCapacitadorByNombreAsync(string capacitador, Guid? excluirCapacitacionId = null, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Dictionary<Guid, Capacitacion> Stored { get; } = new();

        public Task<IReadOnlyList<Capacitacion>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Capacitacion>)Stored.Values.ToList());

        public Task<Capacitacion?> GetByIdWithResponsablesAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Stored.TryGetValue(id, out var c) ? c : null);

        public Task<string> AddAsync(Capacitacion entity, Func<CancellationToken, Task<string>> codeFactory, CancellationToken ct = default)
        {
            Stored[entity.Id] = entity;
            return Task.FromResult(entity.Codigo);
        }

        public Task UpdateWithResponsablesAsync(Capacitacion entity, IEnumerable<CapacitacionResponsable> nuevasRelaciones, CancellationToken ct = default)
        {
            Stored[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Capacitacion entity, CancellationToken ct = default)
        {
            Stored[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteLogicoAsync(Guid id, CancellationToken ct = default)
        {
            if (Stored.TryGetValue(id, out var c)) c.Activo = false;
            return Task.CompletedTask;
        }

        public Task<int> GetMaxCodigoNumberAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    internal sealed class InMemoryLogoStorage : ILogoCapacitacionStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = new();

        public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct)
        {
            var name = $"{Guid.NewGuid():N}.{extension}";
            using var ms = new MemoryStream();
            await contenido.CopyToAsync(ms, ct);
            Saved[name] = ms.ToArray();
            return name;
        }

        public Task EliminarAsync(string logoPath, CancellationToken ct)
        {
            Saved.Remove(logoPath);
            return Task.CompletedTask;
        }
    }
}
