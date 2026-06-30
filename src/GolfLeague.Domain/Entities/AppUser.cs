using Microsoft.AspNetCore.Identity;

namespace GolfLeague.Domain.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public string? TotpSecret { get; set; }
    public bool TotpEnabled { get; set; }

    public bool IsSuperAdmin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Roles are stored in AspNetUserRoles (Identity's join table). Use
    // UserManager.GetRolesAsync / AddToRoleAsync / RemoveFromRoleAsync to
    // read or mutate. A user can hold any combination of admin / scorer /
    // player.
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<UserPasskey> Passkeys { get; set; } = [];
}
