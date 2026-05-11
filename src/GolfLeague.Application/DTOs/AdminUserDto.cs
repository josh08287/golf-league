namespace GolfLeague.Application.DTOs;

/// <summary>
/// AppUser as seen by the admin user-management UI. Only used for accounts
/// that aren't linked to a Player row (i.e. league administrators who
/// don't compete). Players-with-accounts continue to surface via PlayerDto.
/// </summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    bool HasPassword,
    bool HasTotp,
    bool HasPasskey,
    bool IsLockedOut);
