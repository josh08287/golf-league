namespace GolfLeague.Domain.Entities;

/// <summary>
/// A single participant's team assignment within a team-based side game
/// (currently just 2v2 best ball). Unused for non-team games like Nassau.
/// Team sizes are not required to be balanced — a group short a player can
/// still play 1v3 or 1v2.
/// </summary>
public class TeeTimeSideGameTeam
{
    public int Id { get; set; }

    public int TeeTimeSideGameId { get; set; }
    public TeeTimeSideGame SideGame { get; set; } = null!;

    /// <summary>1 or 2.</summary>
    public int TeamNumber { get; set; }

    public int ParticipantId { get; set; }
    public RoundParticipant Participant { get; set; } = null!;
}
