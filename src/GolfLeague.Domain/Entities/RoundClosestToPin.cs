namespace GolfLeague.Domain.Entities;

/// <summary>
/// Records which player was closest to the pin on a par-3 hole of a round.
/// At most one row per (round, hole); no row means no winner was selected
/// ("None"). Unlike TournamentHoleExtra this applies to regular league
/// rounds, entered by a scorer/admin from the score entry screen.
/// </summary>
public class RoundClosestToPin
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int HoleNumber { get; set; }
    public int PlayerId { get; set; }

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
