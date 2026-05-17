namespace GolfLeague.Domain.Entities;

/// <summary>
/// Stores admin-recorded extras per hole in a tournament round:
/// closest-to-the-pin on par 3s, and longest drive on designated holes.
/// </summary>
public class TournamentHoleExtra
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int HoleNumber { get; set; }

    /// <summary>Player who hit closest-to-the-pin on this par 3 hole (null if not recorded).</summary>
    public int? ClosestToPinPlayerId { get; set; }

    /// <summary>Player who hit the longest drive on this hole (null if not recorded).</summary>
    public int? LongestDrivePlayerId { get; set; }

    public Round Round { get; set; } = null!;
    public Player? ClosestToPinPlayer { get; set; }
    public Player? LongestDrivePlayer { get; set; }
}
