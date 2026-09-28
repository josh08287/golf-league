using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ITeeTimeSideGameRepository
{
    /// <summary>
    /// Returns every side game a tee time has opted into, with Teams eagerly
    /// loaded.
    /// </summary>
    Task<IReadOnlyList<TeeTimeSideGame>> GetForTeeTimeAsync(int teeTimeId, CancellationToken cancellationToken = default);

    Task<TeeTimeSideGame?> GetByIdAsync(int sideGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opts a tee time into a game, inserting its team rows (if any) in the
    /// same save. Fails at the DB level (unique index) if already opted in
    /// to this game type — callers should check <see cref="GetForTeeTimeAsync"/>
    /// first to give a clean error instead.
    /// </summary>
    Task<TeeTimeSideGame> AddAsync(TeeTimeSideGame sideGame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opts a tee time out of a game (removes the TeeTimeSideGame row; its
    /// Team rows cascade-delete with it).
    /// </summary>
    Task RemoveAsync(int sideGameId, CancellationToken cancellationToken = default);
}
