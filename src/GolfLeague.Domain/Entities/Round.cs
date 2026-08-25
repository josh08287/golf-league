using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class Round
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public int SeasonId { get; set; }
    public int? HalfId { get; set; }
    public int CourseId { get; set; }
    public int WeekNumber { get; set; }
    public DateOnly RoundDate { get; set; }
    public RoundStatus Status { get; set; } = RoundStatus.Scheduled;
    public NineHoleSide NineHoleSide { get; set; } = NineHoleSide.Front;
    public RoundType RoundType { get; set; } = RoundType.NineHole;
    public string? Notes { get; set; }

    /// <summary>
    /// Tournament rounds only: the hole (never a par 3) the admin designated
    /// for the longest-drive award. Null until configured; longest-drive
    /// selection is unavailable until it's set.
    /// </summary>
    public int? LongestDriveHoleNumber { get; set; }

    /// <summary>
    /// Tournament rounds only: the dollar pool paid out across all gross
    /// skins won this round, split evenly per skin (pool / total skins won).
    /// Null when no gross skins game is being run.
    /// </summary>
    public decimal? GrossSkinsPool { get; set; }

    /// <summary>
    /// Tournament rounds only: the dollar pool paid out across all net
    /// skins won this round, split evenly per skin (pool / total skins won).
    /// Null when no net skins game is being run.
    /// </summary>
    public decimal? NetSkinsPool { get; set; }

    /// <summary>
    /// UTC instant the sign-up reminder email was sent for this round, or
    /// null if not yet sent. Guards against the hourly autofill timer
    /// re-sending the reminder on every run within its window.
    /// </summary>
    public DateTime? SignUpReminderSentAt { get; set; }

    /// <summary>
    /// UTC instant the weekly tee-time schedule email was automatically sent
    /// for this round by the autofill timer, or null if not yet sent. Guards
    /// against the hourly timer re-sending the schedule email on every run
    /// after autofill (autofill itself doesn't transition Round.Status, so
    /// a round can stay a "candidate" for hours). Deliberately NOT checked
    /// by the admin "resend" endpoint — that action should always be able to
    /// re-send on demand regardless of this flag.
    /// </summary>
    public DateTime? TeeTimeScheduleEmailSentAt { get; set; }

    /// <summary>
    /// UTC instant the substitute-pool "spots available" email was sent for
    /// this round, or null if not yet sent. Sent alongside the sign-up
    /// reminder (same window, before autofill) but only when at least one
    /// participant has skipped, opening a spot. Guards against re-sending on
    /// every hourly timer run within the window.
    /// </summary>
    public DateTime? SubSpotEmailSentAt { get; set; }

    public Season Season { get; set; } = null!;
    public SeasonHalf? Half { get; set; }
    public Course Course { get; set; } = null!;
    public ICollection<RoundParticipant> Participants { get; set; } = [];
    public ICollection<FlightMatch> FlightMatches { get; set; } = [];
    public ICollection<TournamentFlight> TournamentFlights { get; set; } = [];
    public ICollection<TournamentMatchup> TournamentMatchups { get; set; } = [];
    public ICollection<TournamentHoleExtra> TournamentHoleExtras { get; set; } = [];
    public ICollection<TournamentLongestDriveWinner> TournamentLongestDriveWinners { get; set; } = [];
    public ICollection<RoundClosestToPin> ClosestToPinWinners { get; set; } = [];
}
