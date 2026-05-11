namespace GolfLeague.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    // Optional until a user is attached. Admins can create Player rows for
    // people who haven't been invited yet; once an AppUser is linked, the
    // email is set to match the AppUser's email.
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    // Nullable: a Player row can exist without an AppUser. Admins create
    // players who may never log in (only have scores recorded for them).
    // The AppUser carries the authoritative Role for authorization.
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }

    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{GetFirstChar(FirstName)}{GetFirstChar(LastName)}".ToUpperInvariant();

    private static string GetFirstChar(string name) =>
        string.IsNullOrEmpty(name) ? "" : name[..1];

    public ICollection<FlightMembership> FlightMemberships { get; set; } = [];
    public ICollection<Handicap> Handicaps { get; set; } = [];
    public ICollection<RoundParticipant> RoundParticipants { get; set; } = [];
}
