using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Interfaces;

/// <summary>
/// Read/lookup access to AppUser plus role mutation. Create/password/login
/// flows still go through ASP.NET Core Identity's UserManager directly in
/// the Infrastructure layer.
/// </summary>
public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, PlayerRole>> GetRolesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
    Task UpdateRoleAsync(Guid userId, PlayerRole role, CancellationToken cancellationToken = default);
}
