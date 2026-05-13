using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests;

public class HandicapCalculationServiceTests
{
    [Fact]
    public void CalculateNewIndex_WhenEmpty_ReturnsZero()
    {
        HandicapCalculationService.CalculateNewIndex(Array.Empty<double>()).Should().Be(0.0);
    }

    [Theory]
    [InlineData(new[] { 10.0 }, 10.0)]                          // 1 diff: avg of 1
    [InlineData(new[] { 8.0, 10.0 }, 9.0)]                     // 2 diffs: (8+10)/2
    [InlineData(new[] { 5.0, 8.0, 10.0 }, 7.7)]                // 3 diffs: 23/3 = 7.666... → 7.7
    [InlineData(new[] { 5.0, 6.0, 8.0, 10.0 }, 7.2)]           // 4 diffs: 29/4 = 7.25 → 7.2 (ToEven)
    [InlineData(new[] { 2.0, 4.0, 6.0, 8.0, 10.0 }, 6.0)]     // 5 diffs: avg = 6
    public void CalculateNewIndex_SimpleAverage(double[] diffs, double expected)
    {
        HandicapCalculationService.CalculateNewIndex(diffs).Should().BeApproximately(expected, 0.05);
    }

    [Fact]
    public void CalculateNewIndex_IgnoresBeyondRollingWindow()
    {
        // 6 diffs supplied; only first 5 (newest) should be averaged.
        // Caller passes newest-first: [2,4,6,8,10,100]. Window of 5 = [2,4,6,8,10], avg = 6.
        var diffs = new[] { 2.0, 4.0, 6.0, 8.0, 10.0, 100.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(6.0);
    }

    [Fact]
    public void CalculateNewIndex_RoundsToOneDecimal()
    {
        // (1+2+3)/3 = 2.0 — exact
        var diffs = new[] { 1.0, 2.0, 3.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(2.0);
    }

    [Fact]
    public void CalculateNewIndex_RollingWindowSize_IsFive()
    {
        HandicapCalculationService.RollingWindowSize.Should().Be(5);
    }
}

public class StablefordScoringServiceTests
{
    [Theory]
    [InlineData(10.0, 113, 10)]  // exact: 10*113/113=10
    [InlineData(18.0, 113, 18)]
    [InlineData(0.0, 113, 0)]
    [InlineData(10.0, 130, 12)] // 10*130/113=11.50... rounds away from zero -> 12
    public void CourseHandicap_ReturnsCorrectValue(double index, int slope, int expected)
    {
        StablefordScoringService.CourseHandicap(index, slope).Should().Be(expected);
    }

    [Theory]
    [InlineData(18, 1, 1)]  // 18/18=1, 1<=18%18=0 -> 0, total=1
    [InlineData(18, 18, 1)] // 18/18=1, 18<=18%18=0 -> 0, total=1
    [InlineData(18, 19, 1)] // 18/18=1, 19>0 -> 0, total=1
    [InlineData(19, 1, 2)]  // floor(19/18)=1, 1<=19%18=1 -> 1, total=2
    [InlineData(0, 1, 0)]   // 0 strokes
    [InlineData(9, 9, 1)]   // floor(9/18)=0, 9<=9%18=9 -> 1
    [InlineData(9, 10, 0)]  // floor(9/18)=0, 10>9%18=9 -> 0
    public void StrokesOnHole_ReturnsCorrectValue(int courseHandicap, int strokeIndex, int expected)
    {
        StablefordScoringService.StrokesOnHole(courseHandicap, strokeIndex).Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 1, 3)]
    [InlineData(5, 2, 3)]
    [InlineData(3, 0, 3)]
    public void NetStrokes_SubtractsHandicapStrokes(int gross, int handicapStrokes, int expected)
    {
        StablefordScoringService.NetStrokes(gross, handicapStrokes).Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 4, 2)]  // par 4, net par => 2 points
    [InlineData(4, 3, 3)]  // par 4, net birdie => 3 points
    [InlineData(4, 2, 4)]  // par 4, net eagle => 4 points
    [InlineData(4, 1, 5)]  // par 4, net albatross => 5 points
    [InlineData(4, 0, 6)]  // par 4, net condor => 6 (max)
    [InlineData(4, 5, 1)]  // par 4, net bogey => 1 point
    [InlineData(4, 6, 0)]  // par 4, net double bogey => 0 points
    [InlineData(4, 7, 0)]  // par 4, worse => 0 (clamped)
    [InlineData(4, -1, 6)] // eagle+1, clamped to 6
    public void StablefordPoints_ReturnsCorrectPoints(int par, int netStrokes, int expected)
    {
        StablefordScoringService.StablefordPoints(par, netStrokes).Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 0, 6)]  // par+2+0
    [InlineData(4, 1, 7)]  // par+2+1
    [InlineData(3, 0, 5)]  // par+2+0
    [InlineData(5, 2, 9)]  // par+2+2
    public void MaxGross_ReturnsParPlusTwoPlusHandicapStrokes(int par, int strokesOnHole, int expected)
    {
        StablefordScoringService.MaxGross(par, strokesOnHole).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 7, 4, 1)]   // gross >= max (7 >= 7)
    [InlineData(false, 6, 4, 1)]  // gross < max (6 < 7)
    public void IsNetDoubleBogey_ReturnsCorrectValue(bool expected, int gross, int par, int strokesOnHole)
    {
        StablefordScoringService.IsNetDoubleBogey(gross, par, strokesOnHole).Should().Be(expected);
    }

    [Theory]
    [InlineData(80, 72.0, 113, 8.0)]
    [InlineData(72, 72.0, 113, 0.0)]
    [InlineData(90, 72.0, 130, 15.646)]
    public void ScoreDifferential_ReturnsCorrectValue(int grossStrokes, double courseRating, int slopeRating, double expected)
    {
        var result = StablefordScoringService.ScoreDifferential(grossStrokes, courseRating, slopeRating);
        result.Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData(40, 72.0, 113, 4.0)]   // (40-36)*113/113 = 4.0
    [InlineData(36, 72.0, 113, 0.0)]   // (36-36)*1=0
    [InlineData(44, 72.0, 113, 8.0)]   // (44-36)*1=8.0
    public void NineHoleScoreDifferential_ReturnsCorrectValue(int grossStrokes, double courseRating, int slopeRating, double expected)
    {
        var result = StablefordScoringService.NineHoleScoreDifferential(grossStrokes, courseRating, slopeRating);
        result.Should().BeApproximately(expected, 0.01);
    }
}
