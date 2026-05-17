using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class PlayerInvite
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public string Email { get; set; } = string.Empty;

    // Opaque token embedded in the invite link — cryptographically random, URL-safe
    public string Token { get; set; } = string.Empty;

    public InviteStatus Status { get; set; } = InviteStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string InvitedByUserId { get; set; } = string.Empty;

    // Populated when the invited person signs in and accepts
    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedByAppUserId { get; set; }

    public Domain.Enums.PlayerRole Role { get; set; } = Domain.Enums.PlayerRole.Player;

    // Optional: admin pre-selected a Player profile that should be attached
    // to the AppUser created at acceptance time. When set, it bypasses the
    // email-match adopt path and the new-Player fallback.
    public int? PreLinkedPlayerId { get; set; }
    public Player? PreLinkedPlayer { get; set; }

    // Linked player record created on acceptance
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
}

