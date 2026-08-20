using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ITeeTimeRepository
{
    /// <summary>
    /// Returns all tee times for a round, ordered by TeeTimeNumber, with
    /// Participants + their Player eagerly loaded.
    /// </summary>
    Task<IReadOnlyList<RoundTeeTime>> GetByRoundAsync(int roundId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one tee time including its participants. Used by join/leave
    /// flows that need to count occupants and check capacity.
    /// </summary>
    Task<RoundTeeTime?> GetByIdAsync(int teeTimeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup for several tee times at once (no Participants graph —
    /// callers needing that should use <see cref="GetByIdAsync"/> or
    /// <see cref="GetByRoundAsync"/>). Use this instead of calling
    /// <see cref="GetByIdAsync"/> in a loop — the per-tee-time loop was a
    /// source of avoidable SQL round trips on the audit log page.
    /// </summary>
    Task<IReadOnlyList<RoundTeeTime>> GetByIdsAsync(IEnumerable<int> teeTimeIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create slot 1..N for a round in a single transaction. Returns the
    /// inserted rows. Idempotent: skips slots that already exist.
    /// </summary>
    Task<IReadOnlyList<RoundTeeTime>> EnsureSlotsAsync(int roundId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a participant's TeeTimeId. Pass null to clear. Tracked save inside.
    /// </summary>
    Task SetParticipantTeeTimeAsync(int participantId, int? teeTimeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchange the TeeTimeId of two participants in one save, so swapping
    /// two players between (possibly full) slots never touches capacity.
    /// </summary>
    Task SwapParticipantTeeTimesAsync(int participantAId, int participantBId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a tee time as auto-filled at the supplied UTC instant.
    /// </summary>
    Task MarkAutoFilledAsync(int teeTimeId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies every participant → tee-time assignment and stamps every
    /// touched slot's AutoFilledAt in a single tracked load + one
    /// SaveChangesAsync. Use this instead of calling
    /// <see cref="SetParticipantTeeTimeAsync"/>/<see cref="MarkAutoFilledAsync"/>
    /// in a loop — the autofill timer previously issued 2 round trips per
    /// participant placed plus 2 per touched slot, every hour, for every
    /// round in the autofill window.
    /// </summary>
    Task ApplyAutofillAsync(
        IReadOnlyDictionary<int, int> teeTimeIdByParticipantId,
        IEnumerable<int> touchedSlotIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set (or clear, when null) the shotgun-start hole for a tee time group.
    /// </summary>
    Task SetStartingHoleAsync(int teeTimeId, int? startingHoleNumber, CancellationToken cancellationToken = default);
}
