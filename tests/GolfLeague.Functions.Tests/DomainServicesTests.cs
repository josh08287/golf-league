using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests;

public class HandicapCalculationServiceTests
{
    [Theory]
    [InlineData(10.0, 12.0, 11.0)]
    [InlineData(5.5, 6.5, 6.0)]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(20.0, 24.0, 22.0)]
    public void CombineNineHoleDifferentials_ReturnsAverage(double d1, double d2, double expected)
    {
        HandicapCalculationService.CombineNineHoleDifferentials(d1, d2).Should().Be(expected);
    }

    [Fact]
    public void CombineNineHoleDifferentials_RoundsTwoDecimalPlaces()
    {
        var result = HandicapCalculationService.CombineNineHoleDifferentials(10.1, 10.2);
        result.Should().Be(10.15);
    }

    [Fact]
    public void CalculateNewIndex_WhenEmpty_ReturnsZero()
    {
        HandicapCalculationService.CalculateNewIndex([]).Should().Be(0.0);
    }

    [Fact]
    public void CalculateNewIndex_With1Differential_UsesIt()
    {
        // 1 diff => bestCount=1, uses the 1 value; 10.0 * 0.96 = 9.6
        var result = HandicapCalculationService.CalculateNewIndex([10.0]);
        result.Should().Be(9.6);
    }

    [Fact]
    public void CalculateNewIndex_With2Differentials_UsesLowest1()
    {
        // 2 diffs => bestCount=1, lowest is 8.0; 8.0 * 0.96 = 7.68 → 7.7
        var result = HandicapCalculationService.CalculateNewIndex([10.0, 8.0]);
        result.Should().Be(7.7);
    }

    [Fact]
    public void CalculateNewIndex_With3Differentials_UsesLowest1()
    {
        // 3 diffs => bestCount=1, lowest is 5.0; 5.0 * 0.96 = 4.8
        var result = HandicapCalculationService.CalculateNewIndex([10.0, 8.0, 5.0]);
        result.Should().Be(4.8);
    }

    [Fact]
    public void CalculateNewIndex_With5Differentials_UsesLowest2()
    {
        // 5 diffs => bestCount=2, lowest two are 5.0 and 6.0; avg=5.5; 5.5*0.96=5.28→5.3
        var result = HandicapCalculationService.CalculateNewIndex([10.0, 8.0, 5.0, 6.0, 9.0]);
        result.Should().Be(5.3);
    }

    [Fact]
    public void CalculateNewIndex_With7Differentials_UsesLowest3()
    {
        // 7 diffs => bestCount=3
        var diffs = new double[] { 10.0, 9.0, 8.0, 7.0, 6.0, 5.0, 4.0 };
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // Lowest 3: 4.0, 5.0, 6.0 => avg=5.0; 5.0*0.96=4.8
        result.Should().Be(4.8);
    }

    [Fact]
    public void CalculateNewIndex_With9Differentials_UsesLowest4()
    {
        // 9 diffs => bestCount=4
        var diffs = Enumerable.Range(1, 9).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // Lowest 4: 1,2,3,4 => avg=2.5; 2.5*0.96=2.4
        result.Should().Be(2.4);
    }

    [Fact]
    public void CalculateNewIndex_With11Differentials_UsesLowest5()
    {
        var diffs = Enumerable.Range(1, 11).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // Lowest 5: 1,2,3,4,5 => avg=3.0; 3.0*0.96=2.88→2.9
        result.Should().Be(2.9);
    }

    [Fact]
    public void CalculateNewIndex_With14Differentials_UsesLowest6()
    {
        var diffs = Enumerable.Range(1, 14).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // Lowest 6: 1..6 => avg=3.5; 3.5*0.96=3.36→3.4
        result.Should().Be(3.4);
    }

    [Fact]
    public void CalculateNewIndex_With17Differentials_UsesLowest7()
    {
        var diffs = Enumerable.Range(1, 17).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // Lowest 7: 1..7 => avg=4.0; 4.0*0.96=3.84→3.8
        result.Should().Be(3.8);
    }

    [Fact]
    public void CalculateNewIndex_With20Differentials_UsesLowest8()
    {
        var diffs = Enumerable.Range(1, 20).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // Lowest 8: 1..8 => avg=4.5; 4.5*0.96=4.32→4.3
        result.Should().Be(4.3);
    }

    [Fact]
    public void CalculateNewIndex_IgnoresDifferentialsOlderThan20()
    {
        // 25 diffs => only last 20 used
        var diffs = Enumerable.Range(1, 25).Select(i => (double)i).ToList();
        // Last 20 are: 6..25. Lowest 8: 6..13 => avg=9.5; 9.5*0.96=9.12→9.1
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        result.Should().Be(9.1);
    }

    [Fact]
    public void CalculateNewIndex_NeverBelowNegative10()
    {
        // Very negative differentials should be clamped to -10
        var diffs = Enumerable.Repeat(-20.0, 20).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        result.Should().Be(-10.0);
    }

    [Fact]
    public void CalculateNewIndex_WithBankruptPlusCourseChange_UsesEvenRounding()
    {
        // Test banker rounding (ToEven)
        // Average = 5.25, * 0.96 = 5.04 => rounds to 5.0
        var diffs = new double[] { 5.25, 5.25 };
        var result = HandicapCalculationService.CalculateNewIndex(diffs);
        // 1 diff used (count=2 -> bestCount=1), lowest=5.25; 5.25*0.96=5.04 -> 5.0
        result.Should().Be(5.0);
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
