using System.Text;
using Capacitaciones.Application.Dtos.Recursos;
using Capacitaciones.Application.Ports;
using Capacitaciones.Application.UseCases.Recursos;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Tests;

/// <summary>
/// Tests unitarios de los casos de uso del módulo Repositorio. Usan repos en memoria
/// y un <see cref="InMemoryResourceStorage"/> — sin EF ni HTTP.
/// </summary>
public class RecursoUseCasesTests
{
    [Fact]
    public async Task Subir_HappyPath_PersisteArchivoYMetadata()
    {
        var repo = new InMemoryRecursoRepository();
        var storage = new InMemoryResourceStorage();
        var useCase = new SubirRecursoUseCase(repo, storage);

        var bytes = Encoding.UTF8.GetBytes("hola repositorio");
        using var ms = new MemoryStream(bytes);

        var dto = await useCase.ExecuteAsync(
            ms,
            bytes.Length,
            "guia.pdf",
            nombreUsuario: "Guía rápida",
            descripcion: "Documento introductorio.",
            contentType: "application/pdf");

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Guía rápida", dto.NombreOriginal);
        Assert.Equal("pdf", dto.Extension);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(bytes.Length, dto.TamanoBytes);
        Assert.True(dto.Activo);
        Assert.EndsWith(".pdf", dto.NombreAlmacenado);
        Assert.Single(repo.Stored);
        Assert.True(storage.Saved.ContainsKey(dto.NombreAlmacenado));
    }

    [Fact]
    public async Task Subir_ExtensionExe_Rechaza()
    {
        var useCase = NewUseCase(out _, out _);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var ex = await Assert.ThrowsAsync<RecursoServiceException>(() =>
            useCase.ExecuteAsync(ms, 3, "malware.exe", null, "descripcion", null));

        Assert.Equal("EXTENSION_PROHIBIDA", ex.Codigo);
    }

    [Fact]
    public async Task Subir_ExtensionSh_Rechaza()
    {
        var useCase = NewUseCase(out _, out _);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var ex = await Assert.ThrowsAsync<RecursoServiceException>(() =>
            useCase.ExecuteAsync(ms, 3, "script.sh", null, "descripcion", null));

        Assert.Equal("EXTENSION_PROHIBIDA", ex.Codigo);
    }

    [Fact]
    public async Task Subir_ArchivoMayorA100MB_Rechaza()
    {
        var useCase = NewUseCase(out _, out _);

        using var ms = new MemoryStream(new byte[] { 0 });
        // Simulamos tamaño reportado >100 MB (el stream real puede ser chico en tests).
        var ex = await Assert.ThrowsAsync<RecursoServiceException>(() =>
            useCase.ExecuteAsync(ms, 100_000_001, "grande.bin", null, "descripcion", null));

        Assert.Equal("ARCHIVO_DEMASIADO_GRANDE", ex.Codigo);
    }

    [Fact]
    public async Task Subir_SinDescripcion_Rechaza()
    {
        var useCase = NewUseCase(out _, out _);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var ex = await Assert.ThrowsAsync<RecursoServiceException>(() =>
            useCase.ExecuteAsync(ms, 3, "doc.txt", null, "   ", null));

        Assert.Equal("DESCRIPCION_REQUERIDA", ex.Codigo);
    }

    [Fact]
    public async Task Subir_ArchivoVacio_Rechaza()
    {
        var useCase = NewUseCase(out _, out _);

        using var ms = new MemoryStream(Array.Empty<byte>());
        var ex = await Assert.ThrowsAsync<RecursoServiceException>(() =>
            useCase.ExecuteAsync(ms, 0, "vacio.txt", null, "descripcion", null));

        Assert.Equal("ARCHIVO_VACIO", ex.Codigo);
    }

    [Fact]
    public async Task EditarMetadata_ActualizaCamposYFechaActualizacion()
    {
        var repo = new InMemoryRecursoRepository();
        var storage = new InMemoryResourceStorage();
        var existing = new Recurso
        {
            Id = Guid.NewGuid(),
            NombreOriginal = "original.txt",
            NombreAlmacenado = Guid.NewGuid().ToString("N") + ".txt",
            Extension = "txt",
            ContentType = "text/plain",
            TamanoBytes = 100,
            Descripcion = "vieja",
            Activo = true,
            FechaCreacion = DateTime.UtcNow.AddHours(-1),
            FechaActualizacion = null
        };
        await repo.AddAsync(existing);

        var useCase = new EditarMetadataRecursoUseCase(repo, storage);
        var dto = await useCase.ExecuteAsync(existing.Id, new UpdateRecursoMetadataDto
        {
            NombreOriginal = "nuevo-nombre.txt",
            Descripcion = "descripción actualizada"
        });

        Assert.Equal("nuevo-nombre.txt", dto.NombreOriginal);
        Assert.Equal("descripción actualizada", dto.Descripcion);
        Assert.NotNull(dto.FechaActualizacion);
    }

    [Fact]
    public async Task EditarMetadata_ConArchivoNuevo_ReemplazaBinarioYBorraViejo()
    {
        var repo = new InMemoryRecursoRepository();
        var storage = new InMemoryResourceStorage();
        var oldStored = Guid.NewGuid().ToString("N") + ".txt";
        storage.Saved[oldStored] = Encoding.UTF8.GetBytes("contenido viejo");

        var existing = new Recurso
        {
            Id = Guid.NewGuid(),
            NombreOriginal = "original.txt",
            NombreAlmacenado = oldStored,
            Extension = "txt",
            ContentType = "text/plain",
            TamanoBytes = 15,
            Descripcion = "vieja",
            Activo = true,
            FechaCreacion = DateTime.UtcNow.AddHours(-1),
            FechaActualizacion = null
        };
        await repo.AddAsync(existing);

        var useCase = new EditarMetadataRecursoUseCase(repo, storage);
        var nuevoBytes = Encoding.UTF8.GetBytes("contenido nuevo PDF");
        using var ms = new MemoryStream(nuevoBytes);

        var dto = await useCase.ExecuteAsync(
            existing.Id,
            new UpdateRecursoMetadataDto
            {
                NombreOriginal = "renombrado.pdf",
                Descripcion = "descripción con archivo reemplazado"
            },
            archivoNuevo: ms,
            tamanoNuevo: nuevoBytes.Length,
            nombreArchivoNuevo: "subido.pdf",
            contentTypeNuevo: "application/pdf");

        Assert.Equal("renombrado.pdf", dto.NombreOriginal);
        Assert.Equal("pdf", dto.Extension);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(nuevoBytes.Length, dto.TamanoBytes);
        Assert.NotEqual(oldStored, dto.NombreAlmacenado);
        Assert.True(storage.Saved.ContainsKey(dto.NombreAlmacenado));
        Assert.False(storage.Saved.ContainsKey(oldStored));
    }

    [Fact]
    public async Task EditarMetadata_ConArchivoExtensionProhibida_Rechaza()
    {
        var repo = new InMemoryRecursoRepository();
        var storage = new InMemoryResourceStorage();
        var existing = new Recurso
        {
            Id = Guid.NewGuid(),
            NombreOriginal = "original.txt",
            NombreAlmacenado = Guid.NewGuid().ToString("N") + ".txt",
            Extension = "txt",
            ContentType = "text/plain",
            TamanoBytes = 1,
            Descripcion = "x",
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        };
        await repo.AddAsync(existing);

        var useCase = new EditarMetadataRecursoUseCase(repo, storage);
        using var ms = new MemoryStream(new byte[] { 1, 2 });
        var ex = await Assert.ThrowsAsync<RecursoServiceException>(() =>
            useCase.ExecuteAsync(
                existing.Id,
                new UpdateRecursoMetadataDto { NombreOriginal = "n", Descripcion = "d" },
                archivoNuevo: ms,
                tamanoNuevo: 2,
                nombreArchivoNuevo: "malware.exe",
                contentTypeNuevo: "application/octet-stream"));

        Assert.Equal("EXTENSION_PROHIBIDA", ex.Codigo);
    }

    [Fact]
    public async Task Eliminar_MarcaInactivoYBorraArchivoFisico()
    {
        var repo = new InMemoryRecursoRepository();
        var storage = new InMemoryResourceStorage();
        var storedName = "abcd.txt";
        storage.Saved[storedName] = new byte[] { 1, 2, 3 };

        var entity = new Recurso
        {
            Id = Guid.NewGuid(),
            NombreOriginal = "doc.txt",
            NombreAlmacenado = storedName,
            Extension = "txt",
            ContentType = "text/plain",
            TamanoBytes = 3,
            Descripcion = "x",
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        await repo.AddAsync(entity);

        var useCase = new EliminarRecursoUseCase(repo, storage);
        await useCase.ExecuteAsync(entity.Id);

        var despues = await repo.GetByIdAsync(entity.Id);
        Assert.NotNull(despues);
        Assert.False(despues!.Activo);
        Assert.False(storage.Saved.ContainsKey(storedName));
    }

    [Fact]
    public async Task Subir_CompensaBorrandoArchivoSiRepoFalla()
    {
        var repo = new ThrowingRecursoRepository();
        var storage = new InMemoryResourceStorage();
        var useCase = new SubirRecursoUseCase(repo, storage);

        using var ms = new MemoryStream(new byte[] { 9, 9, 9 });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(ms, 3, "doc.txt", null, "descripcion", "text/plain"));

        // No debió quedar archivo huérfano en el storage tras la compensación.
        Assert.Empty(storage.Saved);
    }

    // ----- Helpers -----

    private static SubirRecursoUseCase NewUseCase(out InMemoryRecursoRepository repo, out InMemoryResourceStorage storage)
    {
        repo = new InMemoryRecursoRepository();
        storage = new InMemoryResourceStorage();
        return new SubirRecursoUseCase(repo, storage);
    }

    private sealed class InMemoryRecursoRepository : IRecursoRepository
    {
        public Dictionary<Guid, Recurso> Stored { get; } = new();

        public Task AddAsync(Recurso entity, CancellationToken ct = default)
        {
            Stored[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Recurso>> ListAsync(bool includeInactive, CancellationToken ct = default)
        {
            IEnumerable<Recurso> q = Stored.Values;
            if (!includeInactive) q = q.Where(r => r.Activo);
            return Task.FromResult((IReadOnlyList<Recurso>)q.OrderByDescending(r => r.FechaCreacion).ToList());
        }

        public Task<Recurso?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Stored.TryGetValue(id, out var r) ? r : null);

        public Task UpdateAsync(Recurso entity, CancellationToken ct = default)
        {
            Stored[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            if (Stored.TryGetValue(id, out var r) && r.Activo)
            {
                r.Activo = false;
                r.FechaActualizacion = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>Repo que lanza en AddAsync para validar la compensación del UseCase.</summary>
    private sealed class ThrowingRecursoRepository : IRecursoRepository
    {
        public Task AddAsync(Recurso entity, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
        public Task<IReadOnlyList<Recurso>> ListAsync(bool includeInactive, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Recurso>)new List<Recurso>());
        public Task<Recurso?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Recurso?>(null);
        public Task UpdateAsync(Recurso entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class InMemoryResourceStorage : IResourceStorage
    {
        public Dictionary<string, byte[]> Saved { get; } = new();

        public async Task SaveAsync(Stream content, string storedName, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            Saved[storedName] = ms.ToArray();
        }

        public bool Exists(string storedName) => Saved.ContainsKey(storedName);

        public Task DeleteAsync(string storedName, CancellationToken ct)
        {
            Saved.Remove(storedName);
            return Task.CompletedTask;
        }

        public Stream OpenRead(string storedName)
        {
            if (!Saved.TryGetValue(storedName, out var bytes))
                throw new FileNotFoundException(storedName);
            return new MemoryStream(bytes, writable: false);
        }

        public string GetAbsolutePath(string storedName) => $"/in-memory/{storedName}";
    }
}
