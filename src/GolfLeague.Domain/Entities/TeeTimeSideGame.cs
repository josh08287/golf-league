using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

/// <summary>
/// An optional side game (Nassau, 2v2 best ball, Bingo Bango Bongo, Wolf)
/// a tee-time group has opted into for their round. Every player still
/// enters their own gross/net score exactly as normal — opting in never
/// changes how strokes are recorded. Nassau and 2v2 best ball are scored
/// purely from those HoleScore rows; Bingo Bango Bongo and Wolf need a few
/// extra per-hole facts that aren't part of a scorecard (who was first on
/// the green, who the Wolf picked as a partner), captured in
/// <see cref="HolePicks"/> / <see cref="WolfPicks"/>.
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

    /// <summary>
    /// Team assignments for 2v2 best ball and team Nassau. Wolf repurposes
    /// this same table to store its rotation order instead — TeamNumber
    /// holds the 1-based rotation position rather than a team side; see
    /// WolfScoringService for how the rotation is read back.
    /// </summary>
    public ICollection<TeeTimeSideGameTeam> Teams { get; set; } = [];

    /// <summary>Bingo Bango Bongo only: per-hole honor winners.</summary>
    public ICollection<TeeTimeSideGameHolePick> HolePicks { get; set; } = [];

    /// <summary>Wolf only: per-hole partner/lone-wolf calls.</summary>
    public ICollection<TeeTimeWolfHolePick> WolfPicks { get; set; } = [];
}
