using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IHandicapRepository
{
    Task<Handicap?> GetCurrentAsync(int playerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Handicap>> GetHistoryAsync(int playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The player's last 20 9-hole score differentials, newest first. Derived
    /// from finalized round participants (TotalGrossStrokes + the course's
    /// rating/slope). Returns fewer than 20 if the player hasn't played that
    /// many.
    /// </summary>
    Task<IReadOnlyList<double>> GetLast20NineHoleDifferentialsAsync(int playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The player's lowest handicap index across all Handicap rows with an
    /// EffectiveDate within the last 365 days. Used for WHS soft / hard cap.
    /// Returns null when the player has no qualifying history.
    /// </summary>
    Task<double?> GetLowIndexInLast365DaysAsync(int playerId, DateOnly asOf, CancellationToken cancellationToken = default);

    Task AddAsync(Handicap handicap, CancellationToken cancellationToken = default);
}
