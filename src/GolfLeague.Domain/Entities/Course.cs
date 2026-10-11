namespace GolfLeague.Domain.Entities;

public class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CourseRating { get; set; }
    public int SlopeRating { get; set; }

    /// <summary>Course centre point, used to seed the OpenStreetMap hole import.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ICollection<CourseHole> Holes { get; set; } = [];
    public ICollection<TeeBox> TeeBoxes { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
    public ICollection<CourseHoleGeo> HoleGeos { get; set; } = [];
}
