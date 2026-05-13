namespace GolfLeague.Domain.Services;

/// <summary>
/// League handicap calculation.
///
/// Rule: after each weekly 9-hole round, the player's handicap is the simple
/// average of their last up to <see cref="RollingWindowSize"/> 9-hole score
/// differentials (most-recent rounds first). The seed handicap (manual /
/// initial entry not tied to a round) is the starting index for new players
/// and is never modified by this service.
/// </summary>
public static class HandicapCalculationService
{
    /// <summary>Number of recent rounds used for the rolling average.</summary>
    public const int RollingWindowSize = 5;

    /// <summary>
    /// Compute a new handicap index as the simple average of the supplied
    /// 9-hole score differentials.
    /// </summary>
    /// <param name="recentDifferentials">
    /// The player's last <see cref="RollingWindowSize"/> 9-hole score
    /// differentials, newest first. Caller should supply at most
    /// <see cref="RollingWindowSize"/> values; any extras are ignored.
    /// Returns 0.0 when the list is empty.
    /// </param>
    public static double CalculateNewIndex(IReadOnlyList<double> recentDifferentials)
    {
        if (recentDifferentials.Count == 0) return 0.0;

        var pool = recentDifferentials.Count <= RollingWindowSize
            ? recentDifferentials
            : recentDifferentials.Take(RollingWindowSize).ToList();

        return Math.Round(pool.Average(), 1, MidpointRounding.ToEven);
    }
}
