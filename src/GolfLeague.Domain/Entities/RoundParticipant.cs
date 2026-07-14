namespace GolfLeague.Domain.Entities;

public class RoundParticipant
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int PlayerId { get; set; }
    public int? FlightId { get; set; } // Null for tournament rounds (no flight grouping)
    public double HandicapIndex { get; set; }
    public int CourseHandicap { get; set; }
    public int? TotalGrossStrokes { get; set; }
    public int? TotalNetStrokes { get; set; }
    public int? TotalGrossStablefordPoints { get; set; }
    public int? TotalNetStablefordPoints { get; set; }
    public bool IsWithdrawn { get; set; }

    /// <summary>
    /// True when the player explicitly skipped the week. Counts as a played
    /// round with 0 Stableford points for season standings, but is excluded
    /// from handicap differential calculations.
    /// </summary>
    public bool SkippedWeek { get; set; }

    /// <summary>
    /// Optional tee-time assignment. Null until the player claims a slot
    /// (or autofill places them). Used by the /tee-times sign-up flow.
    /// </summary>
    public int? TeeTimeId { get; set; }
    public RoundTeeTime? TeeTime { get; set; }

    /// <summary>
    /// True when this participant is a substitute filling in for a skipped
    /// player (weekly/half rounds) or an admin-added extra for a tournament
    /// round. Scored and kept in history, but excluded from season
    /// standings, points, handicap differentials, skins, and closest-to-pin.
    /// </summary>
    public bool IsSubstitute { get; set; } = false;

    /// <summary>
    /// For weekly/half rounds only: the skipped participant this substitute
    /// is filling a spot for. Bookkeeping only (the round-wide skip cap is
    /// what's enforced, not a strict 1:1 slot linkage). Null for tournament
    /// substitutes, which have no skipped participant to link back to.
    /// </summary>
    public int? SubstituteForParticipantId { get; set; }
    public RoundParticipant? SubstituteForParticipant { get; set; }

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Flight? Flight { get; set; }
    public ICollection<HoleScore> HoleScores { get; set; } = [];
}
