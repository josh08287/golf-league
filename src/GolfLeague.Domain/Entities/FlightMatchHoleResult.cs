namespace GolfLeague.Domain.Entities;

/// <summary>
/// Per-hole point outcome for both players in a FlightMatch, computed by
/// MatchPlayScoringService (standard) or IMatchPlayFormulaEvaluator (custom)
/// once both players' HoleScore rows exist for the week (or one/both are
/// absent). Persisted so standings/leaderboard reads don't need to
/// re-evaluate scoring per request, and so a later change to the half's
/// custom formula doesn't retroactively rewrite historical weeks' results.
/// </summary>
public class FlightMatchHoleResult
{
    public int Id { get; set; }
    public int FlightMatchId { get; set; }
    public int HoleNumber { get; set; }
    public int Player1Points { get; set; }
    public int Player2Points { get; set; }

    /// <summary>True when this hole was scored against the card because the opponent was absent/on a bye.</summary>
    public bool IsAgainstCard { get; set; }

    public FlightMatch FlightMatch { get; set; } = null!;
}
