namespace GolfLeague.Domain.Entities;

/// <summary>
/// A player's default tee box for one course during one season half. A half
/// isn't tied to a single course, so the default is stored per course. Kept
/// out of FlightMembership, whose rows churn on flight reassignment.
/// </summary>
public class PlayerHalfTeeBox
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int HalfId { get; set; }
    public int CourseId { get; set; }
    public int TeeBoxId { get; set; }

    public Player Player { get; set; } = null!;
    public SeasonHalf Half { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public TeeBox TeeBox { get; set; } = null!;
}
