namespace GolfLeague.Domain.Entities;

public class League
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // URL-safe slug used as the subdomain identifier, e.g. "riverside-golf"
    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Season> Seasons { get; set; } = [];
    public ICollection<Player> Players { get; set; } = [];
    public ICollection<LeagueMembership> Memberships { get; set; } = [];
}
