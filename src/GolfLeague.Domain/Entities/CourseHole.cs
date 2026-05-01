namespace GolfLeague.Domain.Entities;

public class CourseHole
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int HoleNumber { get; set; }
    public int Par { get; set; }
    public int StrokeIndex { get; set; }

    public Course Course { get; set; } = null!;
}
