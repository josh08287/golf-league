using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ISeasonRepository
{
    Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Season season, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int seasonId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
