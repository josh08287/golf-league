using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IHandicapRepository
{
    Task<Handicap?> GetCurrentAsync(int playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Handicap>> GetHistoryAsync(int playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<double>> GetLast20DifferentialsAsync(int playerId, CancellationToken cancellationToken = default);
    Task AddAsync(Handicap handicap, CancellationToken cancellationToken = default);
}
