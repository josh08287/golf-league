using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

/// <summary>
/// An optional side game (Nassau, 2v2 best ball, etc.) a tee-time group has
/// opted into for their round. Every game in this system is scored purely
/// from each player's own HoleScore rows — no game ever combines strokes
/// into a shared team ball, so opting in never changes how scores are
/// entered, only how they're aggregated for display.
/// </summary>
public class TeeTimeSideGame
{
    public int Id { get; set; }

    public int TeeTimeId { get; set; }
    public RoundTeeTime TeeTime { get; set; } = null!;

    public SideGameType GameType { get; set; }

    /// <summary>
    /// Gross or net scoring basis. Only meaningful for Nassau; ignored for
    /// team games like 2v2 best ball, which always compare net (matching
    /// how match-play formats are scored elsewhere in this app).
    /// </summary>
    public ScoringBasis ScoringBasis { get; set; }

    /// <summary>
    /// Nassau only: whether the group is playing 2v2 team Nassau (best-ball
    /// per side, uses <see cref="Teams"/>) or individual Nassau (every
    /// opted-in player's pairwise 1v1 match against every other, ignores
    /// <see cref="Teams"/>). Ignored for 2v2 best ball, which is always
    /// team-based.
    /// </summary>
    public NassauFormat? NassauFormat { get; set; }

    public int OptedInByPlayerId { get; set; }
    public DateTime OptedInAt { get; set; }

    public ICollection<TeeTimeSideGameTeam> Teams { get; set; } = [];
}
