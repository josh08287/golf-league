namespace GolfLeague.Domain.Entities;

/// <summary>
/// An emoji reaction placed on a tournament comment by a player.
/// Each player may only have one reaction of a given emoji per comment.
/// </summary>
public class CommentReaction
{
    public int Id { get; set; }
    public int CommentId { get; set; }
    public int PlayerId { get; set; }
    /// <summary>The emoji codepoint, e.g. "👍", "🔥", "⛳".</summary>
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public TournamentComment Comment { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
