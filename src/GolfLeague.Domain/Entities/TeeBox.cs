namespace GolfLeague.Domain.Entities;

public class TeeBox
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "Championship", "Men's", "Women's", "Forward"
    public double CourseRating { get; set; }
    public int SlopeRating { get; set; }
    public int TotalYardage { get; set; }
    public int Par { get; set; }

    /// <summary>
    /// The course's default tee. Exactly one per course (enforced by a
    /// filtered unique index). Players with no per-half default for the
    /// course play from this tee.
    /// </summary>
    public bool IsDefault { get; set; }
    public ICollection<HoleTeeBox> HoleTeeBoxes { get; set; } = [];
    public Course Course { get; set; } = null!;
}
