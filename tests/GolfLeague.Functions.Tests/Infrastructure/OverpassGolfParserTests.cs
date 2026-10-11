using FluentAssertions;
using GolfLeague.Infrastructure.Geo;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

public class OverpassGolfParserTests
{
    // Hole 1 runs due north into a square green; hole 2 has no mapped green;
    // a second "hole 1" 5 km away belongs to another course; one hole line
    // has no ref at all.
    private const string Fixture = """
        {
          "elements": [
            { "type": "way", "id": 101, "tags": { "golf": "hole", "ref": "1", "par": "4" },
              "geometry": [ { "lat": 40.0000, "lon": -75.0000 }, { "lat": 40.0015, "lon": -75.0000 }, { "lat": 40.0030, "lon": -75.0000 } ] },
            { "type": "way", "id": 201, "tags": { "golf": "green" },
              "geometry": [ { "lat": 40.0030, "lon": -75.0001 }, { "lat": 40.0030, "lon": -74.9999 },
                            { "lat": 40.0032, "lon": -74.9999 }, { "lat": 40.0032, "lon": -75.0001 },
                            { "lat": 40.0030, "lon": -75.0001 } ] },
            { "type": "way", "id": 102, "tags": { "golf": "hole", "ref": "2" },
              "geometry": [ { "lat": 40.0040, "lon": -75.0010 }, { "lat": 40.0040, "lon": -75.0040 } ] },
            { "type": "way", "id": 901, "tags": { "golf": "hole", "ref": "1" },
              "geometry": [ { "lat": 40.0450, "lon": -75.0000 }, { "lat": 40.0480, "lon": -75.0000 } ] },
            { "type": "way", "id": 902, "tags": { "golf": "hole" },
              "geometry": [ { "lat": 40.0010, "lon": -75.0010 }, { "lat": 40.0020, "lon": -75.0010 } ] },
            { "type": "node", "id": 5, "lat": 40.0, "lon": -75.0 }
          ]
        }
        """;

    [Fact]
    public void Parse_UsesFirstNodeAsTee_AndGreenPolygonCentroidAsGreen()
    {
        var result = OverpassGolfParser.Parse(Fixture, 40.0015, -75.0010);

        var hole1 = result.Holes.Single(h => h.HoleNumber == 1);
        hole1.WayId.Should().Be(101);
        hole1.Par.Should().Be(4);
        hole1.Tee.Lat.Should().Be(40.0000);
        hole1.Green.Lat.Should().BeApproximately(40.0031, 1e-9);
        hole1.Green.Lng.Should().BeApproximately(-75.0000, 1e-9);
    }

    [Fact]
    public void Parse_FrontAndBackFollowTheLineOfPlay()
    {
        var result = OverpassGolfParser.Parse(Fixture, 40.0015, -75.0010);

        var hole1 = result.Holes.Single(h => h.HoleNumber == 1);
        hole1.GreenFront!.Lat.Should().Be(40.0030);
        hole1.GreenBack!.Lat.Should().Be(40.0032);
    }

    [Fact]
    public void Parse_WithoutGreenPolygon_UsesEndOfHoleLine()
    {
        var result = OverpassGolfParser.Parse(Fixture, 40.0015, -75.0010);

        var hole2 = result.Holes.Single(h => h.HoleNumber == 2);
        hole2.Green.Lat.Should().Be(40.0040);
        hole2.Green.Lng.Should().Be(-75.0040);
        hole2.GreenFront.Should().BeNull();
        hole2.GreenBack.Should().BeNull();
        hole2.Par.Should().BeNull();
    }

    [Fact]
    public void Parse_DuplicateRef_KeepsTheHoleNearestTheCentre_AndWarns()
    {
        var result = OverpassGolfParser.Parse(Fixture, 40.0015, -75.0010);

        result.Holes.Where(h => h.HoleNumber == 1).Should().ContainSingle().Which.WayId.Should().Be(101);
        result.Warnings.Should().Contain(w => w.Contains("more than one hole 1"));
    }

    [Fact]
    public void Parse_HoleWithoutRef_IsSkippedWithWarning()
    {
        var result = OverpassGolfParser.Parse(Fixture, 40.0015, -75.0010);

        result.Holes.Select(h => h.HoleNumber).Should().BeEquivalentTo([1, 2]);
        result.Warnings.Should().Contain(w => w.Contains("way 902"));
    }

    [Fact]
    public void Parse_EmptyResponse_ReturnsNoHoles()
    {
        var result = OverpassGolfParser.Parse("""{ "elements": [] }""", 40, -75);

        result.Holes.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }
}
