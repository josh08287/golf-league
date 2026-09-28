namespace GolfLeague.Domain.Entities;

/// <summary>
/// Records one hole's Wolf call: who the Wolf was (derived from the
/// rotation set at opt-in, stored here for auditability), whether they went
/// it alone, and who their partner was if not.
/// </summary>
public class TeeTimeWolfHolePick
{
    public int Id { get; set; }

    public int TeeTimeSideGameId { get; set; }
    public TeeTimeSideGame SideGame { get; set; } = null!;

    public int HoleNumber { get; set; }

    public int WolfParticipantId { get; set; }
    public RoundParticipant WolfParticipant { get; set; } = null!;

    public bool IsLoneWolf { get; set; }

    /// <summary>Null when IsLoneWolf is true.</summary>
    public int? PartnerParticipantId { get; set; }
    public RoundParticipant? PartnerParticipant { get; set; }

    public int RecordedByPlayerId { get; set; }
    public DateTime RecordedAt { get; set; }
}
