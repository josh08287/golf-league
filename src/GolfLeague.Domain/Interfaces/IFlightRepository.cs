using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IFlightRepository
{
    Task<Flight?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flight>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flight>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlightMembership>> GetMembershipsAsync(int flightId, CancellationToken cancellationToken = default);
    /// <summary>All flight memberships for every flight within a half, keyed by player via PlayerId.</summary>
    Task<IReadOnlyList<FlightMembership>> GetMembershipsByHalfAsync(int halfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetStandingsAsync(int flightId, int halfId, CancellationToken cancellationToken = default);
    Task AddAsync(Flight flight, CancellationToken cancellationToken = default);
    Task AddHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default);
    Task UpdateHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default);
    Task<SeasonHalf?> GetHalfByIdAsync(int halfId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight batch lookup (no Flights/Memberships graph) for resolving
    /// several halves' display names at once. Use this instead of calling
    /// <see cref="GetHalfByIdAsync"/> in a loop — the per-half loop was a
    /// source of avoidable SQL round trips on the audit log page.
    /// </summary>
    Task<IReadOnlyList<SeasonHalf>> GetHalvesByIdsAsync(IEnumerable<int> halfIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeasonHalf>> GetHalvesBySeasonAsync(int seasonId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int flightId, CancellationToken cancellationToken = default);
    Task AddMembershipAsync(FlightMembership membership, CancellationToken cancellationToken = default);
    Task<int?> GetActiveSeasonIdAsync(CancellationToken cancellationToken = default);
    /// <summary>Returns true if the half has any InProgress, PendingFinalization, or Finalized rounds.</summary>
    Task<bool> IsHalfLockedAsync(int halfId, CancellationToken cancellationToken = default);
}
