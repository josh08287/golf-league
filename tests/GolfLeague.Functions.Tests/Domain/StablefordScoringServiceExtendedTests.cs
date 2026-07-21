using Xunit;
using GolfLeague.Domain.Services;
using FluentAssertions;

namespace GolfLeague.Tests.Domain;

public class StablefordScoringServiceTests
{
    [Theory]
    [InlineData(10, 113, 72.0, 72, 10)]
    [InlineData(18, 113, 72.0, 72, 18)]
    [InlineData(0, 113, 72.0, 72, 0)]
    [InlineData(10, 130, 72.0, 72, 12)]
    public void CourseHandicap_ReturnsCorrectValue(double index, int slope, double courseRating, int par, int expected)
    {
        var result = StablefordScoringService.CourseHandicap(index, slope, courseRating, par);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(18, 18, 1)]
    [InlineData(18, 19, 1)]
    [InlineData(18, 1, 1)]
    [InlineData(9, 9, 1)]
    [InlineData(9, 10, 0)]
    [InlineData(19, 1, 2)]
    public void StrokesOnHole_ReturnsCorrectValue(int courseHandicap, int strokeIndex, int expected)
    {
        var result = StablefordScoringService.StrokesOnHole(courseHandicap, strokeIndex);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 1, 3)]
    [InlineData(5, 2, 3)]
    [InlineData(3, 0, 3)]
    public void NetStrokes_SubtractsHandicapStrokes(int gross, int handicapStrokes, int expected)
    {
        var result = StablefordScoringService.NetStrokes(gross, handicapStrokes);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 0, 6)]
    [InlineData(4, 2, 4)]
    [InlineData(4, 3, 3)]
    [InlineData(4, 4, 2)]
    [InlineData(4, 5, 1)]
    [InlineData(4, 6, 0)]
    [InlineData(4, 7, 0)]
    [InlineData(4, -1, 6)]
    public void StablefordPoints_ReturnsCorrectPoints(int par, int netStrokes, int expected)
    {
        var result = StablefordScoringService.StablefordPoints(par, netStrokes);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(6, 4, 1, false)]
    [InlineData(7, 4, 1, true)]
    public void IsNetDoubleBogey_ReturnsCorrectValue(int gross, int par, int strokesOnHole, bool expected)
    {
        var result = StablefordScoringService.IsNetDoubleBogey(gross, par, strokesOnHole);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 1, 7)]
    [InlineData(5, 2, 9)]
    [InlineData(3, 0, 5)]
    public void MaxGross_ReturnsParPlusTwoPlusHandicapStrokes(int par, int strokesOnHole, int expected)
    {
        var result = StablefordScoringService.MaxGross(par, strokesOnHole);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(72, 72, 113, 0)]
    [InlineData(80, 72, 113, 8)]
    [InlineData(90, 72, 130, 15.646)]
    public void ScoreDifferential_ReturnsCorrectValue(int grossStrokes, double courseRating, int slopeRating, double expected)
    {
        var result = StablefordScoringService.ScoreDifferential(grossStrokes, courseRating, slopeRating);
        result.Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData(36, 72, 113, 0)]
    [InlineData(40, 72, 113, 4)]
    [InlineData(44, 72, 113, 8)]
    public void NineHoleScoreDifferential_ReturnsCorrectValue(int grossStrokes, double courseRating, int slopeRating, double expected)
    {
        var result = StablefordScoringService.NineHoleScoreDifferential(grossStrokes, courseRating, slopeRating);
        result.Should().BeApproximately(expected, 0.01);
    }
}
