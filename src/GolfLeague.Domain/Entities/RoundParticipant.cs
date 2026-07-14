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

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Flight? Flight { get; set; }
    public ICollection<HoleScore> HoleScores { get; set; } = [];
}
