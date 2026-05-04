using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IFlightRepository
{
    Task<Flight?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flight>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlightMembership>> GetMembershipsAsync(int flightId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetStandingsAsync(int flightId, int seasonId, CancellationToken cancellationToken = default);
    Task AddAsync(Flight flight, CancellationToken cancellationToken = default);
    Task AddHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default);
    Task DeleteAsync(int flightId, CancellationToken cancellationToken = default);
    Task<int?> GetActiveSeasonIdAsync(CancellationToken cancellationToken = default);
}
