namespace GolfLeague.Domain.Entities;

public class HoleTeeBox
{
    public int TeeBoxId { get; set; }
    public int CourseHoleId { get; set; }
    public int Yardage { get; set; }
    public int Par { get; set; }

    public TeeBox TeeBox { get; set; } = null!;
    public CourseHole CourseHole { get; set; } = null!;
}
