namespace GolfLeague.Domain.Entities;

/// <summary>
/// A handicap-based grouping of a tournament round's players, used only to
/// scope the longest-drive award (one winner per flight). Auto-computed
/// whenever the round's roster changes — split into as many flights as the
/// season's nearest half currently has, ordered low-to-high handicap.
/// Unrelated to the season/half-scoped Flight entity, which tournament
/// rounds (no HalfId) can't reference directly.
/// </summary>
public class TournamentFlight
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int FlightNumber { get; set; }
    public string Name { get; set; } = string.Empty;

    public Round Round { get; set; } = null!;
    public ICollection<RoundParticipant> Participants { get; set; } = [];
}
