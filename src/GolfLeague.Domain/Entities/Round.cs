using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class Round
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int FlightId { get; set; }
    public int CourseId { get; set; }
    public DateOnly RoundDate { get; set; }
    public RoundStatus Status { get; set; } = RoundStatus.Scheduled;
    public RoundType RoundType { get; set; } = RoundType.NineHole;
    public NineHoleSide NineHoleSide { get; set; } = NineHoleSide.Front;
    public string? Notes { get; set; }

    public Season Season { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public ICollection<RoundParticipant> Participants { get; set; } = [];
}
