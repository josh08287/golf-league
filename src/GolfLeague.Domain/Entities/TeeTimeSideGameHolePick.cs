using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

/// <summary>
/// Records who won one of the 3 individual honors on one hole of a Bingo
/// Bango Bongo game. A null WinnerParticipantId means the honor was
/// recorded as having no clear winner for that hole (nobody gets the
/// point) — distinct from the row simply not existing yet (not recorded).
/// </summary>
public class TeeTimeSideGameHolePick
{
    public int Id { get; set; }

    public int TeeTimeSideGameId { get; set; }
    public TeeTimeSideGame SideGame { get; set; } = null!;

    public int HoleNumber { get; set; }
    public BbbHonor Honor { get; set; }

    public int? WinnerParticipantId { get; set; }
    public RoundParticipant? WinnerParticipant { get; set; }

    public int RecordedByPlayerId { get; set; }
    public DateTime RecordedAt { get; set; }
}
