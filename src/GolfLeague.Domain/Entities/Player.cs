using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

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

    /// <summary>
    /// Bit-flags of the player's preferred tee-time slots for autofill.
    /// None = no preference; autofill may place the player in any slot.
    /// </summary>
    public TeeTimeSlotPreference PreferredTeeTimeSlots { get; set; } = TeeTimeSlotPreference.None;

    /// <summary>
    /// When true the player has opted out of tee-time-related emails (the
    /// weekly schedule email and the sign-up reminder email).
    /// Default false = opted in (emails are sent if the league setting is enabled).
    /// </summary>
    public bool TeeTimeEmailOptOut { get; set; } = false;

    /// <summary>
    /// When true, this player is part of the league's substitute pool rather
    /// than a regular roster player. Substitutes may be added to a round's
    /// tee time (in place of a player who skipped) but never accrue season
    /// standings/points/handicap. Kept mutually exclusive with regular
    /// roster status at the command layer (a player with an active
    /// FlightMembership cannot be flagged as a substitute). Can be true even
    /// when IsActive is false, since deactivated players remain sub-eligible.
    /// </summary>
    public bool IsSubstitute { get; set; } = false;

    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{GetFirstChar(FirstName)}{GetFirstChar(LastName)}".ToUpperInvariant();

    private static string GetFirstChar(string name) =>
        string.IsNullOrEmpty(name) ? "" : name[..1];

    public ICollection<FlightMembership> FlightMemberships { get; set; } = [];
    public ICollection<Handicap> Handicaps { get; set; } = [];
    public ICollection<RoundParticipant> RoundParticipants { get; set; } = [];
    public ICollection<PlayerHalfSetting> HalfSettings { get; set; } = [];
}
