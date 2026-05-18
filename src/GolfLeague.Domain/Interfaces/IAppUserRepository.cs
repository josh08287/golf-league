using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

/// <summary>
/// Read/lookup access to AppUser. Create / password / role-mutation flows
/// go through ASP.NET Core Identity's UserManager in the Infrastructure
/// layer (it owns the AspNetUserRoles join table).
/// </summary>
public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-fetch roles for a set of users in one round-trip. Returns a map
    /// of UserId → lowercase role names (e.g. ["admin", "player"]). Users not
    /// in the result have no roles.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRolesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default);
}
