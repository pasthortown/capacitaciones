using Capacitaciones.Application.Dtos.Admin;
using Capacitaciones.Application.Ports;
using Capacitaciones.Domain.Entities;

namespace Capacitaciones.Application.UseCases.Admin;

/// <summary>Caso de uso: crear un nuevo administrador.</summary>
public class CrearAdminUseCase
{
    private const int NombresMaxLength = 255;
    private const int PasswordMinLength = 8;

    private readonly IAdminUserRepository _repo;
    private readonly IPasswordHasher _hasher;

    public CrearAdminUseCase(IAdminUserRepository repo, IPasswordHasher hasher)
    {
        _repo = repo;
        _hasher = hasher;
    }

    public async Task<AdminUserDto> ExecuteAsync(CreateAdminUserDto input, CancellationToken ct = default)
    {
        if (input is null) throw new AuthServiceException("INVALID_INPUT", "Payload requerido.");

        var email = (input.Email ?? string.Empty).Trim();
        if (!AdminEmailPolicy.IsValid(email))
        {
            throw new AuthServiceException(
                "INVALID_EMAIL",
                $"El email debe ser un correo corporativo {AdminEmailPolicy.RequiredDomain}.");
        }

        var nombres = (input.Nombres ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombres))
        {
            throw new AuthServiceException("EMPTY_NAME", "El campo 'nombres' es requerido.");
        }
        if (nombres.Length > NombresMaxLength)
        {
            throw new AuthServiceException(
                "NAME_TOO_LONG",
                $"El campo 'nombres' excede el máximo de {NombresMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < PasswordMinLength)
        {
            throw new AuthServiceException(
                "WEAK_PASSWORD",
                $"La contraseña debe tener al menos {PasswordMinLength} caracteres.");
        }

        var existente = await _repo.GetByEmailAsync(email, ct);
        if (existente is not null)
        {
            throw new AuthServiceException(
                "DUPLICATE_EMAIL",
                $"Ya existe un administrador con el email '{email}'.");
        }

        var entity = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(input.Password),
            Nombres = nombres,
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
        Email = user.Email,
        Nombres = user.Nombres,
        Activo = user.Activo,
        FechaCreacion = user.FechaCreacion
    };
}
