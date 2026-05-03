namespace GolfLeague.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EntraObjectId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{GetFirstChar(FirstName)}{GetFirstChar(LastName)}".ToUpperInvariant();

    private static string GetFirstChar(string name) =>
        string.IsNullOrEmpty(name) ? "" : name[..1];

    public ICollection<FlightMembership> FlightMemberships { get; set; } = [];
    public ICollection<Handicap> Handicaps { get; set; } = [];
    public ICollection<RoundParticipant> RoundParticipants { get; set; } = [];
}
