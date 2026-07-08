using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IPlayerHalfSettingRepository
{
    Task<PlayerHalfSetting?> GetAsync(int playerId, int halfId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All settings rows for players in this half. Players with no row yet
    /// are not included — callers should treat a missing row as the default
    /// (opted in) rather than assuming this list is exhaustive.
    /// </summary>
    Task<IReadOnlyList<PlayerHalfSetting>> GetForHalfAsync(int halfId, CancellationToken cancellationToken = default);

    Task SetPar3GrossSkinsOptInAsync(int playerId, int halfId, int seasonId, bool optIn, CancellationToken cancellationToken = default);
}
