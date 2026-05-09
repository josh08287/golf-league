namespace GolfLeague.Domain.Entities;

public class HoleScore
{
    public int Id { get; set; }
    public int ParticipantId { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int StrokeIndex { get; set; }
    public int GrossStrokes { get; set; }
    public int HandicapStrokes { get; set; }
    public int NetStrokes { get; set; }
    public int GrossStablefordPoints { get; set; }
    public int NetStablefordPoints { get; set; }
    public bool IsMaxScore { get; set; }

    public RoundParticipant Participant { get; set; } = null!;
}
