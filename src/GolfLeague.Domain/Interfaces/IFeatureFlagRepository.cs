using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IFeatureFlagRepository
{
    Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task UpsertAsync(string key, bool enabled, CancellationToken cancellationToken = default);
}
