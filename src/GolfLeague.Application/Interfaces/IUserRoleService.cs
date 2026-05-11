using GolfLeague.Application.Common;

namespace GolfLeague.Application.Interfaces;

/// <summary>
/// Application-layer surface for mutating a user's roles. Backed by
/// ASP.NET Core Identity's UserManager in the Infrastructure layer.
/// </summary>
public interface IUserRoleService
{
    /// <summary>
    /// Replace a user's roles with exactly the supplied set. Adds missing
    /// roles, removes ones that aren't in the list. Idempotent.
    /// </summary>
    Task<Result<bool>> SetRolesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);
}
