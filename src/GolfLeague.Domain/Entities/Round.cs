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

    public Season Season { get; set; } = null!;
    public SeasonHalf? Half { get; set; }
    public Course Course { get; set; } = null!;
    public ICollection<RoundParticipant> Participants { get; set; } = [];
    public ICollection<TournamentMatchup> TournamentMatchups { get; set; } = [];
    public ICollection<TournamentHoleExtra> TournamentHoleExtras { get; set; } = [];
    public ICollection<TournamentLongestDriveWinner> TournamentLongestDriveWinners { get; set; } = [];
}
