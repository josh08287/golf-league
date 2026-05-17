namespace GolfLeague.Domain.Entities;

/// <summary>
/// A head-to-head matchup between two players in a tournament round.
/// MatchupNumber identifies the pairing (1 = first pair, 2 = second pair, etc.).
/// WinnerPlayerId is null until scores are finalized.
/// </summary>
public class TournamentMatchup
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int MatchupNumber { get; set; }
    public int Player1Id { get; set; }
    public int Player2Id { get; set; }

    /// <summary>
    /// Null = not yet determined. 0 = halved (tie by net strokes).
    /// Otherwise equals the winning player's ID.
    /// </summary>
    public int? WinnerPlayerId { get; set; }

    public Round Round { get; set; } = null!;
    public Player Player1 { get; set; } = null!;
    public Player Player2 { get; set; } = null!;
}
