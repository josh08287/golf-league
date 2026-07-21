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

    /// <summary>
    /// Attach a Player profile to an existing admin-only AppUser. If a
    /// Player with the same email already exists and is unlinked, that row
    /// is adopted (preserving its history); otherwise a new Player row is
    /// created with the supplied name, initial handicap, and optional flight.
    /// Returns the resulting PlayerDto.
    /// </summary>
    Task<Result<PlayerDto>> AttachPlayerProfileAsync(
        Guid userId,
        string firstName,
        string lastName,
        double initialHandicap,
        int? flightId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually map an existing unlinked Player to an existing AppUser.
    /// Refuses if either side is already linked. Sets Player.Email to match
    /// AppUser.Email (the UI should warn the admin when they differ).
    /// </summary>
    Task<Result<PlayerDto>> LinkPlayerToUserAsync(
        int playerId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear Player.AppUserId. The AppUser record and its league
    /// memberships/roles are untouched — only the player/account link is
    /// cut. Refuses if the player has no linked account.
    /// </summary>
    Task<Result<PlayerDto>> UnlinkPlayerFromUserAsync(
        int playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Account details (MFA, password, lockout, login providers) for a
    /// single AppUser, for display on the player detail page.
    /// </summary>
    Task<AccountInfoDto?> GetAccountInfoAsync(Guid userId, CancellationToken cancellationToken = default);
}
