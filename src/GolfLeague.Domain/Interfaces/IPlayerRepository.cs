using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Player?> GetByAppUserIdAsync(Guid appUserId, CancellationToken cancellationToken = default);
    Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active players with no linked AppUser. Used by admin to pick which
    /// player a new AppUser should map to (either via the player-detail
    /// "Link to user" action or by pre-attaching to an invite).
    /// </summary>
    Task<IReadOnlyList<Player>> GetUnlinkedActiveAsync(CancellationToken cancellationToken = default);

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
