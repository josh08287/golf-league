using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;

namespace GolfLeague.Application.Interfaces;

public interface IAdminUserService
{
    /// <summary>
    /// List AppUsers that aren't linked to a Player row. These are the
    /// league administrators / scorers who don't compete.
    /// </summary>
    Task<IReadOnlyList<AdminUserDto>> ListAdminOnlyUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace the user's roles with exactly the supplied set. Refuses if
    /// the change would drop the last admin in the system.
    /// </summary>
    Task<Result<AdminUserDto>> SetRolesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wipe TOTP secret/enrollment and all passkeys for this user. They will
    /// need to re-enroll a second factor on next admin sign-in.
    /// </summary>
    Task<Result<bool>> ResetMfaAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-delete an AppUser and all dependent rows (passkeys, refresh
    /// tokens, role assignments, external logins). Refuses if it would
    /// delete the last admin.
    /// </summary>
    Task<Result<bool>> DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
