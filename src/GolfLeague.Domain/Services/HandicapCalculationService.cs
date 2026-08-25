namespace GolfLeague.Domain.Services;

/// <summary>
/// League handicap calculation.
///
/// Default rule: after each weekly 9-hole round, the player's handicap is
/// the simple average of their best (lowest) X 9-hole score differentials
/// out of their last Y rounds played (most-recent Y first), WHS-style. X
/// and Y are configurable per league via LeagueSettings
/// (handicap_window_x / handicap_window_y); <see cref="DefaultWindowX"/>
/// and <see cref="DefaultWindowY"/> are the out-of-the-box values. The seed
/// handicap (manual / initial entry not tied to a round) is the starting
/// index for new players and is never modified by this service.
/// </summary>
public static class HandicapCalculationService
{
    /// <summary>Default number of best differentials averaged, out of the last Y rounds.</summary>
    public const int DefaultWindowX = 5;

    /// <summary>Default number of most-recent rounds considered as the candidate pool.</summary>
    public const int DefaultWindowY = 5;

    /// <summary>Back-compat alias for <see cref="DefaultWindowX"/> — the historical fixed rolling window size.</summary>
    public const int RollingWindowSize = DefaultWindowX;

    /// <summary>
    /// Compute a new handicap index as the simple average of the best
    /// (lowest) <paramref name="windowX"/> differentials out of the
    /// supplied recent differentials pool.
    /// </summary>
    /// <param name="recentDifferentials">
    /// The player's most recent 9-hole score differentials, newest first,
    /// already limited to at most <paramref name="windowY"/> rounds by the
    /// caller. Returns 0.0 when the list is empty.
    /// </param>
    /// <param name="windowX">Number of best (lowest) differentials to average. Defaults to <see cref="DefaultWindowX"/>.</param>
    /// <param name="windowY">
    /// Unused here (the caller is expected to have already limited
    /// <paramref name="recentDifferentials"/> to this many rounds); kept as
    /// a parameter so call sites can pass both league settings together.
    /// </param>
    public static double CalculateNewIndex(IReadOnlyList<double> recentDifferentials, int windowX = DefaultWindowX, int windowY = DefaultWindowY)
    {
        if (recentDifferentials.Count == 0) return 0.0;

        var x = Math.Max(1, windowX);

        var pool = recentDifferentials.Count <= x
            ? recentDifferentials
            : recentDifferentials.OrderBy(d => d).Take(x).ToList();

        return Math.Round(pool.Average(), 1, MidpointRounding.ToEven);
    }
}
