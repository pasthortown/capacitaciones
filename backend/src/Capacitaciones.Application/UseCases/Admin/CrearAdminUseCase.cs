using Capacitaciones.Application.Dtos.Admin;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Admin;

/// <summary>Caso de uso: agregar un usuario de red a la lista de permitidos.</summary>
public class CrearAdminUseCase
{
    private const int UsuarioMaxLength = 100;

    private readonly IAdminUserRepository _repo;

    public CrearAdminUseCase(IAdminUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<AdminUserDto> ExecuteAsync(CreateAdminUserDto input, CancellationToken ct = default)
    {
        if (input is null) throw new AuthServiceException("INVALID_INPUT", "Payload requerido.");

        var usuario = (input.UsuarioRed ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(usuario))
            throw new AuthServiceException("EMPTY_USUARIO", "El usuario de red es requerido.");
        if (usuario.Contains('@') || usuario.Contains(' '))
            throw new AuthServiceException("INVALID_USUARIO", "Ingresa el usuario de red (sin @ ni espacios), no el correo.");
        if (usuario.Length > UsuarioMaxLength)
            throw new AuthServiceException("USUARIO_TOO_LONG", $"El usuario de red excede {UsuarioMaxLength} caracteres.");

        var existente = await _repo.GetByUsuarioRedAsync(usuario, ct);
        if (existente is not null)
        {
            if (existente.Activo)
                throw new AuthServiceException("DUPLICATE_USUARIO", $"El usuario de red '{usuario}' ya está en la lista.");
            // Fila inactiva de un borrado lógico previo: la reactivamos (idempotente).
            existente.Activo = true;
            existente.FechaActualizacion = DateTime.UtcNow;
            await _repo.UpdateAsync(existente, ct);
            return ToDto(existente);
        }

        var entity = new AdminUser
        {
            Id = Guid.NewGuid(),
            UsuarioRed = usuario,
            Email = string.Empty,
            PasswordHash = string.Empty,
            Nombres = string.Empty,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            FechaActualizacion = null,
            UltimoLogin = null
        };
        await _repo.AddAsync(entity, ct);

        return ToDto(entity);
    }

    internal static AdminUserDto ToDto(AdminUser user) => new()
    {
        Id = user.Id,
        UsuarioRed = user.UsuarioRed,
        Activo = user.Activo,
        FechaCreacion = user.FechaCreacion,
        UltimoLogin = user.UltimoLogin
    };
}
