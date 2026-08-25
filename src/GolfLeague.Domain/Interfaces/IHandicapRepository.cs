using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

/// <summary>
/// A single qualifying round's raw inputs for computing a 9-hole score
/// differential, before any mode (USGA / straight strokes / custom formula)
/// is applied. See <see cref="GolfLeague.Domain.Services.HandicapFormulaInput"/>.
/// </summary>
public readonly record struct HandicapRoundInput(int GrossStrokes, double CourseRating, int SlopeRating, int Par);

public interface IHandicapRepository
{
    Task<Handicap?> GetCurrentAsync(int playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Handicap>> GetHistoryAsync(int playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every Handicap row across all players, used by bulk recalculation to
    /// look up each player's handicap as of a given round date without
    /// issuing one query per player.
    /// </summary>
    Task<IReadOnlyList<Handicap>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The player's most-recent <paramref name="count"/> qualifying rounds'
    /// raw score inputs (newest first), restricted to finalized rounds on or
    /// before <paramref name="asOfDate"/>. Pass <c>null</c> for
    /// <paramref name="asOfDate"/> to include all rounds. Callers convert
    /// each entry to a differential using the league's configured mode.
    /// </summary>
    Task<IReadOnlyList<HandicapRoundInput>> GetLastNRoundInputsAsync(
        int playerId,
        int count,
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all Handicap rows for <paramref name="playerId"/> whose
    /// <see cref="Handicap.Source"/> is <see cref="HandicapSource.Calculated"/>.
    /// </summary>
    Task DeleteCalculatedAsync(int playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes only the calculated Handicap row(s) for <paramref name="playerId"/>
    /// with the given <paramref name="effectiveDate"/> — used when reopening a
    /// single round so the rest of the player's handicap history is preserved.
    /// </summary>
    Task DeleteCalculatedForDateAsync(int playerId, DateOnly effectiveDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every Handicap row with Source = Calculated across all players.
    /// Used by the admin bulk-recalculation command.
    /// </summary>
    Task DeleteAllCalculatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the distinct player IDs that have at least one finalized round
    /// participation (excluding withdrawals and skipped-week stubs).
    /// </summary>
    Task<IReadOnlyList<int>> GetAllPlayerIdsWithFinalizedRoundsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the dates of all finalized rounds that <paramref name="playerId"/>
    /// participated in (not withdrawn, not skipped, has gross strokes recorded),
    /// ordered oldest-first.
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetFinalizedRoundDatesForPlayerAsync(int playerId, CancellationToken cancellationToken = default);

    Task AddAsync(Handicap handicap, CancellationToken cancellationToken = default);
}
