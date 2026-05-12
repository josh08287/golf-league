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
    /// Create slot 1..N for a round in a single transaction. Returns the
    /// inserted rows. Idempotent: skips slots that already exist.
    /// </summary>
    Task<IReadOnlyList<RoundTeeTime>> EnsureSlotsAsync(int roundId, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a participant's TeeTimeId. Pass null to clear. Tracked save inside.
    /// </summary>
    Task SetParticipantTeeTimeAsync(int participantId, int? teeTimeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a tee time as auto-filled at the supplied UTC instant.
    /// </summary>
    Task MarkAutoFilledAsync(int teeTimeId, DateTime utcNow, CancellationToken cancellationToken = default);
}
