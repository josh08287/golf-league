namespace GolfLeague.Domain.Entities;

public class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CourseRating { get; set; }
    public int SlopeRating { get; set; }

    public ICollection<CourseHole> Holes { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
}
