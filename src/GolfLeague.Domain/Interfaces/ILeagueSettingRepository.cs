using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ILeagueSettingRepository
{
    Task<IReadOnlyList<LeagueSetting>> GetAllAsync(int leagueId, CancellationToken cancellationToken = default);
    Task<LeagueSetting?> GetAsync(int leagueId, string key, CancellationToken cancellationToken = default);
    Task UpsertAsync(int leagueId, string key, string value, CancellationToken cancellationToken = default);
}
