namespace GolfLeague.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// League the session was scoped to when this token was issued. A refresh
    /// that doesn't name a league keeps this one, so a background refresh can't
    /// silently drop league scope (which for a SuperAdmin means every league).
    /// </summary>
    public int? LeagueId { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
