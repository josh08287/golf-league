namespace GolfLeague.Domain.Entities;

/// <summary>
/// Records the player who won the longest drive award, on the round's
/// configured LongestDriveHoleNumber, for one tournament flight. At most one
/// winner per (round, flight); a flight with no row has no winner yet.
/// </summary>
public class TournamentLongestDriveWinner
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int TournamentFlightId { get; set; }
    public int PlayerId { get; set; }

    public Round Round { get; set; } = null!;
    public TournamentFlight TournamentFlight { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
