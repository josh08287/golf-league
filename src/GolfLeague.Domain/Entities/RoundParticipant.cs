namespace GolfLeague.Domain.Entities;

public class RoundParticipant
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int PlayerId { get; set; }
    public int FlightId { get; set; } // Flight at time of round creation
    public double HandicapIndex { get; set; }
    public int CourseHandicap { get; set; }
    public int? TotalGrossStrokes { get; set; }
    public int? TotalNetStrokes { get; set; }
    public int? TotalStablefordPoints { get; set; }
    public bool IsWithdrawn { get; set; }

    public Round Round { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
    public ICollection<HoleScore> HoleScores { get; set; } = [];
}
