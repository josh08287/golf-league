using System.Globalization;
using System.Text.Json;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;

namespace GolfLeague.Infrastructure.Geo;

/// <summary>
/// Turns an Overpass <c>out geom;</c> JSON response of golf=hole and
/// golf=green ways into per-hole tee/green coordinates. Pure (no I/O) so it
/// can be tested against recorded responses.
/// </summary>
public static class OverpassGolfParser
{
    /// <summary>A green further than this from the end of a hole line is assumed to belong to another hole.</summary>
    private const double MaxGreenMatchMeters = 80;

    private sealed record Point(double Lat, double Lng);
    private sealed record Way(long Id, Dictionary<string, string> Tags, List<Point> Geometry);

    public static OsmGolfImportResult Parse(string json, double centreLat, double centreLng)
    {
        var ways = ReadWays(json);
        var warnings = new List<string>();

        var greens = ways
            .Where(w => w.Tags.GetValueOrDefault("golf") == "green" && w.Geometry.Count >= 3)
            .Select(w => Vertices(w.Geometry))
            .ToList();

        var holeWays = new Dictionary<int, Way>();
        foreach (var way in ways.Where(w => w.Tags.GetValueOrDefault("golf") == "hole" && w.Geometry.Count >= 2))
        {
            if (!int.TryParse(way.Tags.GetValueOrDefault("ref"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                warnings.Add($"Skipped an OpenStreetMap hole line (way {way.Id}) with no hole number.");
                continue;
            }

            if (holeWays.TryGetValue(number, out var existing))
            {
                // 27/36-hole facilities reuse refs across their nines/courses;
                // keep whichever line sits nearer the chosen course centre.
                var keep = DistanceMeters(Midpoint(way.Geometry), new Point(centreLat, centreLng))
                    < DistanceMeters(Midpoint(existing.Geometry), new Point(centreLat, centreLng)) ? way : existing;
                holeWays[number] = keep;
                warnings.Add($"Found more than one hole {number} nearby; used the one closest to the course centre.");
                continue;
            }

            holeWays[number] = way;
        }

        var holes = holeWays
            .OrderBy(kv => kv.Key)
            .Select(kv => BuildHole(kv.Key, kv.Value, greens))
            .ToList();

        return new OsmGolfImportResult(holes, warnings);
    }

    private static OsmGolfHole BuildHole(int number, Way way, List<List<Point>> greens)
    {
        var tee = way.Geometry[0];
        var end = way.Geometry[^1];

        var green = greens
            .Select(poly => new { poly, centroid = Centroid(poly) })
            .Select(g => new { g.poly, g.centroid, dist = Contains(g.poly, end) ? 0 : DistanceMeters(g.centroid, end) })
            .Where(g => g.dist <= MaxGreenMatchMeters)
            .OrderBy(g => g.dist)
            .FirstOrDefault();

        GeoPointDto greenCentre = ToDto(green?.centroid ?? end);
        GeoPointDto? front = null, back = null;

        if (green is not null)
        {
            // Front/back = the green's edge points nearest/farthest along the
            // line of play into the green (from the second-to-last point of
            // the hole line, which follows doglegs better than the tee).
            var approach = way.Geometry[^2];
            var (dx, dy) = LocalMeters(approach, green.centroid);
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0)
            {
                dx /= len; dy /= len;
                var projected = green.poly
                    .Select(p => { var (px, py) = LocalMeters(green.centroid, p); return (p, along: px * dx + py * dy); })
                    .ToList();
                front = ToDto(projected.MinBy(x => x.along).p);
                back = ToDto(projected.MaxBy(x => x.along).p);
            }
        }

        int? par = int.TryParse(way.Tags.GetValueOrDefault("par"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : null;
        return new OsmGolfHole(number, ToDto(tee), greenCentre, front, back, par, way.Id);
    }

    private static List<Way> ReadWays(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<Way>();
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return result;

        foreach (var el in elements.EnumerateArray())
        {
            if (el.GetProperty("type").GetString() != "way" || !el.TryGetProperty("geometry", out var geom))
                continue;

            var tags = new Dictionary<string, string>();
            if (el.TryGetProperty("tags", out var t))
                foreach (var prop in t.EnumerateObject())
                    tags[prop.Name] = prop.Value.GetString() ?? string.Empty;

            var points = geom.EnumerateArray()
                .Where(g => g.ValueKind == JsonValueKind.Object)
                .Select(g => new Point(g.GetProperty("lat").GetDouble(), g.GetProperty("lon").GetDouble()))
                .ToList();

            result.Add(new Way(el.GetProperty("id").GetInt64(), tags, points));
        }
        return result;
    }

    /// <summary>Polygon vertices without the duplicated closing point.</summary>
    private static List<Point> Vertices(List<Point> ring) =>
        ring.Count > 1 && ring[0] == ring[^1] ? ring.Take(ring.Count - 1).ToList() : ring;

    private static Point Centroid(List<Point> poly) =>
        new(poly.Average(p => p.Lat), poly.Average(p => p.Lng));

    private static Point Midpoint(List<Point> line) => line[line.Count / 2];

    private static bool Contains(List<Point> poly, Point p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if ((poly[i].Lat > p.Lat) != (poly[j].Lat > p.Lat)
                && p.Lng < (poly[j].Lng - poly[i].Lng) * (p.Lat - poly[i].Lat) / (poly[j].Lat - poly[i].Lat) + poly[i].Lng)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Offset of <paramref name="to"/> from <paramref name="from"/> in metres (east, north). Accurate over a golf hole.</summary>
    private static (double X, double Y) LocalMeters(Point from, Point to)
    {
        const double metersPerDegree = 111_320;
        var x = (to.Lng - from.Lng) * metersPerDegree * Math.Cos(from.Lat * Math.PI / 180);
        var y = (to.Lat - from.Lat) * metersPerDegree;
        return (x, y);
    }

    private static double DistanceMeters(Point a, Point b)
    {
        var (x, y) = LocalMeters(a, b);
        return Math.Sqrt(x * x + y * y);
    }

    private static GeoPointDto ToDto(Point p) => new(p.Lat, p.Lng);
}
