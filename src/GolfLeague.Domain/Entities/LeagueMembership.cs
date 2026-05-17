using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class LeagueMembership
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public PlayerRole Role { get; set; } = PlayerRole.Player;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
