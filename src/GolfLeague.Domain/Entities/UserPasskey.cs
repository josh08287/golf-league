namespace GolfLeague.Domain.Entities;

public sealed class UserPasskey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    // Base64url-encoded WebAuthn credential ID
    public string CredentialId { get; set; } = string.Empty;

    // CBOR-encoded public key (raw bytes)
    public byte[] PublicKey { get; set; } = [];

    public uint SignCount { get; set; }
    public Guid? AaGuid { get; set; }

    // Friendly name set by the user when registering ("My laptop", "iPhone 15")
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
