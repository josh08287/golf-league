using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IPlayerHalfTeeBoxRepository
{
    /// <summary>Every per-half, per-course default for one player.</summary>
    Task<IReadOnlyList<PlayerHalfTeeBox>> GetForPlayerAsync(int playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Defaults for <paramref name="playerIds"/> in one half on one course,
    /// keyed by player ID. Players with no row are absent — callers fall
    /// back to the course's default tee.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetTeeBoxIdsAsync(
        int halfId, int courseId, IEnumerable<int> playerIds, CancellationToken cancellationToken = default);

    /// <summary>Upserts the default, or removes it when <paramref name="teeBoxId"/> is null.</summary>
    Task SetAsync(int playerId, int halfId, int courseId, int? teeBoxId, CancellationToken cancellationToken = default);
}
