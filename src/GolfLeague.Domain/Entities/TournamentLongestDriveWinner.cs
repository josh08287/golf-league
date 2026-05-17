namespace GolfLeague.Domain.Entities;

/// <summary>
/// Records the player(s) who won the longest drive award for a tournament round.
/// Multiple rows are allowed per round (e.g. one per 9-hole side, or tied winners).
/// </summary>
public class TournamentLongestDriveWinner
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int PlayerId { get; set; }

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
