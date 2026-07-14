using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// All players regardless of active status, active players first.
    /// Used by the admin roster view, which lists deactivated players
    /// below the active ones instead of hiding them.
    /// </summary>
    Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Looks up the Player profile for this user within a specific league.
    /// A user may hold one Player row per league, so league scoping is
    /// required to get a unique result.
    /// </summary>
    Task<Player?> GetByAppUserIdAsync(Guid appUserId, int leagueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All Player rows linked to this user across every league. Used for
    /// guards/diagnostics that need to know "does this user have any
    /// profile anywhere," not request handling (which is always league-scoped).
    /// </summary>
    Task<IReadOnlyList<Player>> GetAllByAppUserIdAsync(Guid appUserId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Looks up a Player by email within a specific league. The same email
    /// may have separate Player rows in different leagues (e.g. the same
    /// person belonging to two leagues), so this is league-scoped.
    /// </summary>
    Task<Player?> GetByEmailAsync(string email, int leagueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active players with no linked AppUser. Used by admin to pick which
    /// player a new AppUser should map to (either via the player-detail
    /// "Link to user" action or by pre-attaching to an invite).
    /// </summary>
    Task<IReadOnlyList<Player>> GetUnlinkedActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Substitute-flagged players (active or not — deactivated subs remain
    /// sub-eligible) with no linked AppUser. Used by the invite pre-attach
    /// picker for the substitute pool.
    /// </summary>
    Task<IReadOnlyList<Player>> GetUnlinkedSubstitutesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Player player, CancellationToken cancellationToken = default);
    Task UpdateAsync(Player player, CancellationToken cancellationToken = default);
    Task DeleteAsync(int playerId, CancellationToken cancellationToken = default);
    Task AssignToFlightAsync(int playerId, int? flightId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the player's flight membership for a single half. A non-null
    /// <paramref name="flightId"/> assigns (replacing any existing membership in
    /// that half); a null <paramref name="flightId"/> removes the player from
    /// the half. Other halves are left untouched.
    /// </summary>
    Task SetHalfMembershipAsync(int playerId, int halfId, int? flightId, CancellationToken cancellationToken = default);
}
