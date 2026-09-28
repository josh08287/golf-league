namespace GolfLeague.Domain.Entities;

/// <summary>
/// A message posted by a logged-in player to a tournament round's results
/// page (e.g. trash talk, congratulations). Attributed to the posting
/// player and timestamped; never edited after creation.
/// </summary>
public class TournamentComment
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int PlayerId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
