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

    public Season Season { get; set; } = null!;
    public SeasonHalf? Half { get; set; }
    public Course Course { get; set; } = null!;
    public ICollection<RoundParticipant> Participants { get; set; } = [];
    public ICollection<TournamentMatchup> TournamentMatchups { get; set; } = [];
    public ICollection<TournamentHoleExtra> TournamentHoleExtras { get; set; } = [];
    public ICollection<TournamentLongestDriveWinner> TournamentLongestDriveWinners { get; set; } = [];
    public ICollection<RoundClosestToPin> ClosestToPinWinners { get; set; } = [];
}
