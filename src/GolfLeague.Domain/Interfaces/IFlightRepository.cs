using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IFlightRepository
{
    Task<Flight?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Flight>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoundParticipant>> GetStandingsAsync(int flightId, int seasonId, CancellationToken cancellationToken = default);
}
