namespace GolfLeague.Domain.Entities;

public class RoundParticipant
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int PlayerId { get; set; }
    public int FlightId { get; set; } // Flight at time of round creation (within the half)
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

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
    public ICollection<HoleScore> HoleScores { get; set; } = [];
}
