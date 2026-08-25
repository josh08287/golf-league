using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IFlightMatchRepository
{
    Task<IReadOnlyList<FlightMatch>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlightMatch>> GetByFlightAsync(int flightId, int halfId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FlightMatch>> GetByRoundAsync(int roundId, CancellationToken cancellationToken = default);
    Task<FlightMatch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<FlightMatch> matches, CancellationToken cancellationToken = default);

    /// <summary>Deletes all FlightMatch rows (and their hole results) for a half — used to regenerate the schedule from scratch.</summary>
    Task DeleteByHalfAsync(int halfId, CancellationToken cancellationToken = default);

    Task ReplaceHoleResultsAsync(int flightMatchId, IEnumerable<FlightMatchHoleResult> results, CancellationToken cancellationToken = default);
    Task UpdateMatchTotalsAsync(FlightMatch match, CancellationToken cancellationToken = default);
}
