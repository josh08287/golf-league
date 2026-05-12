namespace GolfLeague.Domain.Entities;

/// <summary>
/// A single tee-time slot within a Round's schedule. Up to 4 RoundParticipants
/// can be linked to one slot. Slots are generated 8 minutes apart starting at
/// 3:28pm on the round date.
/// </summary>
public sealed class RoundTeeTime
{
    public int Id { get; set; }

    public int RoundId { get; set; }
    public Round Round { get; set; } = null!;

    /// <summary>1-based index of this slot within the round (1 = 3:28, 2 = 3:36, …).</summary>
    public int TeeTimeNumber { get; set; }

    /// <summary>Wall-clock tee-off time for this slot.</summary>
    public TimeOnly ScheduledTime { get; set; }

    /// <summary>
    /// Set when the autofill timer fills this slot. Null on slots that were
    /// pre-claimed by a player. Useful for the UI to label autofilled slots
    /// differently and for diagnosis.
    /// </summary>
    public DateTime? AutoFilledAt { get; set; }

    public ICollection<RoundParticipant> Participants { get; set; } = [];
}
