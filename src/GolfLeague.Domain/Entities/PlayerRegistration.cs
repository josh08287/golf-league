using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class PlayerRegistration
{
    public int Id { get; set; }
    public string EntraObjectId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? RejectionReason { get; set; }

    // Set when approved — links to the created Player
    public int? PlayerId { get; set; }
    public Player? Player { get; set; }
}
