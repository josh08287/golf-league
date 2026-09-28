namespace GolfLeague.Domain.Entities;

/// <summary>
/// Durable PKCE state for an in-progress external-login flow (Google/Facebook).
/// Must survive the round trip to the provider's consent screen, which can
/// take long enough for the Functions host to scale or recycle — an
/// in-memory cache does not survive that and was the original storage here.
/// </summary>
public sealed class OAuthFlowState
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string StateKey { get; set; } = string.Empty;
    public string Verifier { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? InviteToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
