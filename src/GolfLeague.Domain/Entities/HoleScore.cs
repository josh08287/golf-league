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

    /// <summary>
    /// Optional: number of putts taken on this hole (used for strokes gained putting).
    /// </summary>
    public int? Putts { get; set; }

    /// <summary>
    /// Optional: distance of the first putt in feet (used for strokes gained putting).
    /// </summary>
    public double? FirstPuttDistanceFeet { get; set; }

    /// <summary>
    /// Whether the player hit the fairway off the tee (relevant for par 4s and 5s).
    /// </summary>
    public bool? FairwayHit { get; set; }

    /// <summary>
    /// Greens in Regulation - true if reached the green in regulation (gross strokes - putts <= par - 2).
    /// Calculated automatically based on score and putts.
    /// </summary>
    public bool? Gir { get; set; }

    /// <summary>
    /// PlayerId of whoever last saved this hole's score (for conflict detection
    /// when multiple group members enter scores for the same player).
    /// </summary>
    public int? LastModifiedByPlayerId { get; set; }

    public RoundParticipant Participant { get; set; } = null!;
}
