using GolfLeague.Application.Common;

namespace GolfLeague.Application.Interfaces;

/// <summary>
/// Service for managing app role assignments in Entra ID (Azure AD).
/// Roles are managed in Entra ID, not the database.
/// </summary>
public interface IEntraRoleService
{
    /// <summary>
    /// Assigns an app role to a user in Entra ID.
    /// </summary>
    /// <param name="userObjectId">The Entra ID object ID of the user</param>
    /// <param name="roleName">The role name (admin, scorer, or player)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<bool>> AssignRoleAsync(string userObjectId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an app role assignment from a user in Entra ID.
    /// </summary>
    /// <param name="userObjectId">The Entra ID object ID of the user</param>
    /// <param name="roleName">The role name to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Result<bool>> RemoveRoleAsync(string userObjectId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all app role assignments for a user.
    /// </summary>
    /// <param name="userObjectId">The Entra ID object ID of the user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of role names assigned to the user</returns>
    Task<Result<List<string>>> GetUserRolesAsync(string userObjectId, CancellationToken cancellationToken = default);
}
