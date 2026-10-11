namespace GolfLeague.Domain.Entities;

/// <summary>
/// Map coordinates for one hole of a course (tee, green centre and optional
/// green front/back), used by the score-entry hole map. Keyed by
/// (CourseId, HoleNumber) rather than hanging off <see cref="CourseHole"/>
/// because CourseHole rows are deleted and re-inserted whenever par/stroke
/// index are edited, which would wipe the coordinates.
/// </summary>
public class CourseHoleGeo
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int HoleNumber { get; set; }

    public double TeeLat { get; set; }
    public double TeeLng { get; set; }
    public double GreenLat { get; set; }
    public double GreenLng { get; set; }
    public double? GreenFrontLat { get; set; }
    public double? GreenFrontLng { get; set; }
    public double? GreenBackLat { get; set; }
    public double? GreenBackLng { get; set; }

    /// <summary>"osm" when imported from OpenStreetMap, "manual" when placed by an admin.</summary>
    public string Source { get; set; } = "manual";
    public long? OsmWayId { get; set; }

    public Course Course { get; set; } = null!;
}
