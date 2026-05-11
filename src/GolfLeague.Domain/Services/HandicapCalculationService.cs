namespace GolfLeague.Domain.Services;

/// <summary>
/// World Handicap System (WHS) calculation as published by the USGA / R&amp;A.
///
/// Inputs are score differentials. This league plays 9-hole rounds, so
/// callers should produce 9-hole differentials via
/// <see cref="StablefordScoringService.NineHoleScoreDifferential"/>.
///
/// The 2020 WHS update treats 9-hole and 18-hole differentials uniformly —
/// 9-hole rounds no longer need to be paired before counting.
///
/// References:
///  - USGA WHS rules of handicapping, rule 5.2 (calculation) and 5.8 (caps).
/// </summary>
public static class HandicapCalculationService
{
    /// <summary>Maximum handicap index permitted by WHS.</summary>
    public const double MaxIndex = 54.0;

    /// <summary>Most negative ("plus") index permitted by WHS.</summary>
    public const double MinIndex = -10.0;

    /// <summary>Soft cap kicks in once the new index would rise more than 3.0 over the 365-day low.</summary>
    public const double SoftCapThreshold = 3.0;

    /// <summary>Hard cap: the new index can never rise more than 5.0 over the 365-day low.</summary>
    public const double HardCapThreshold = 5.0;

    /// <summary>
    /// Compute a new handicap index from the player's differential history.
    /// </summary>
    /// <param name="recentDifferentials">
    /// Up to the player's last 20 score differentials. The method uses the
    /// first 20 of whatever is passed in — caller should pass them ordered
    /// most-recent-first so older differentials are dropped first.
    /// </param>
    /// <param name="lowIndexInLast365Days">
    /// The player's lowest handicap index over the past 365 days. Used for
    /// soft / hard cap. Pass <c>null</c> for new players (no cap applied).
    /// </param>
    public static double CalculateNewIndex(
        IReadOnlyList<double> recentDifferentials,
        double? lowIndexInLast365Days = null)
    {
        if (recentDifferentials.Count == 0) return 0.0;

        var pool = recentDifferentials.Count <= 20
            ? recentDifferentials
            : recentDifferentials.Take(20).ToList();

        var (bestCount, adjustment) = WhsSelection(pool.Count);
        if (bestCount == 0) return 0.0;

        var lowest = pool.OrderBy(d => d).Take(bestCount).ToList();
        var average = lowest.Average();

        var rawIndex = Math.Round(average + adjustment, 1, MidpointRounding.ToEven);

        var capped = ApplyCaps(rawIndex, lowIndexInLast365Days);

        return Math.Clamp(capped, MinIndex, MaxIndex);
    }

    /// <summary>
    /// WHS rule 5.2a: "Score differentials used" table. Returns the number of
    /// lowest differentials to average and an adjustment applied afterward.
    /// </summary>
    public static (int LowestCount, double Adjustment) WhsSelection(int differentialCount) =>
        differentialCount switch
        {
            <= 0 => (0, 0.0),
            // WHS technically requires 3 rounds to issue any index; we behave
            // gracefully for new players and use what exists.
            1 or 2 or 3 => (1, -2.0),
            4 => (1, -1.0),
            5 => (1, 0.0),
            6 => (2, -1.0),
            7 or 8 => (2, 0.0),
            9 or 10 or 11 => (3, 0.0),
            12 or 13 or 14 => (4, 0.0),
            15 or 16 => (5, 0.0),
            17 or 18 => (6, 0.0),
            19 => (7, 0.0),
            >= 20 => (8, 0.0),
        };

    /// <summary>
    /// WHS rule 5.8: soft cap halves the excess above +3 over the 365-day low;
    /// hard cap forbids rising more than +5 above it.
    /// </summary>
    public static double ApplyCaps(double rawIndex, double? lowIndexInLast365Days)
    {
        if (lowIndexInLast365Days is not double low) return rawIndex;

        var rise = rawIndex - low;
        if (rise <= SoftCapThreshold) return rawIndex;

        var excess = rise - SoftCapThreshold;
        var softCapped = low + SoftCapThreshold + (excess / 2.0);

        var hardCeiling = low + HardCapThreshold;
        return Math.Min(softCapped, hardCeiling);
    }
}
