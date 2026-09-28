using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Interfaces;

public interface ITeeTimeSideGameRepository
{
    /// <summary>
    /// Returns every side game a tee time has opted into, with Teams,
    /// HolePicks, and WolfPicks eagerly loaded.
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
    /// Team/HolePick/WolfPick rows cascade-delete with it).
    /// </summary>
    Task RemoveAsync(int sideGameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts one Bingo Bango Bongo honor pick for one hole. Passing a null
    /// winner records "no clear winner" rather than clearing the pick.
    /// </summary>
    Task<TeeTimeSideGameHolePick> UpsertHolePickAsync(
        int sideGameId, int holeNumber, BbbHonor honor, int? winnerParticipantId, int recordedByPlayerId,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts one hole's Wolf call.</summary>
    Task<TeeTimeWolfHolePick> UpsertWolfPickAsync(
        int sideGameId, int holeNumber, int wolfParticipantId, bool isLoneWolf, int? partnerParticipantId, int recordedByPlayerId,
        CancellationToken cancellationToken = default);
}
