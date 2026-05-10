using GolfLeague.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace GolfLeague.Domain.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public PlayerRole Role { get; set; } = PlayerRole.Player;

    public int? PlayerId { get; set; }
    public Player? Player { get; set; }

    public string? TotpSecret { get; set; }
    public bool TotpEnabled { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<UserPasskey> Passkeys { get; set; } = [];
}
