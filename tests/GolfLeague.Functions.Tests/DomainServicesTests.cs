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

    // WHS rule 5.2a — small-sample adjustments. Differentials passed lowest-first
    // for clarity; method picks the lowest regardless of order.
    [Theory]
    [InlineData(new[] { 10.0 }, 8.0)]                   // 1 diff: lowest 1 minus 2
    [InlineData(new[] { 8.0, 10.0 }, 6.0)]              // 2 diffs: lowest 1 minus 2
    [InlineData(new[] { 5.0, 8.0, 10.0 }, 3.0)]         // 3 diffs: lowest 1 minus 2
    [InlineData(new[] { 5.0, 6.0, 8.0, 10.0 }, 4.0)]    // 4 diffs: lowest 1 minus 1
    [InlineData(new[] { 5.0, 6.0, 7.0, 8.0, 10.0 }, 5.0)] // 5 diffs: lowest 1 (no adj)
    public void CalculateNewIndex_SmallSamples_ApplyAdjustment(double[] diffs, double expected)
    {
        HandicapCalculationService.CalculateNewIndex(diffs).Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public void CalculateNewIndex_With6Differentials_UsesLowest2MinusOne()
    {
        // 6 diffs: lowest 2 minus 1.0. Lowest two are 5 and 6, avg 5.5, -1 = 4.5.
        var diffs = new[] { 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(4.5);
    }

    [Fact]
    public void CalculateNewIndex_With8Differentials_UsesLowest2NoAdjustment()
    {
        // 8 diffs: lowest 2. Lowest two are 1 and 2, avg 1.5.
        var diffs = Enumerable.Range(1, 8).Select(i => (double)i).ToArray();
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(1.5);
    }

    [Fact]
    public void CalculateNewIndex_With20Differentials_UsesLowest8()
    {
        // 20 diffs: lowest 8 of 1..20 = avg of 1..8 = 4.5.
        var diffs = Enumerable.Range(1, 20).Select(i => (double)i).ToArray();
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(4.5);
    }

    [Fact]
    public void CalculateNewIndex_DropsBeyond20()
    {
        // 25 diffs in order — method takes only the first 20.
        var diffs = Enumerable.Range(1, 25).Select(i => (double)i).ToArray();
        // First 20 are 1..20, lowest 8 are 1..8, avg 4.5.
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(4.5);
    }

    [Fact]
    public void CalculateNewIndex_ClampsToMinIndex()
    {
        var diffs = Enumerable.Repeat(-50.0, 20).ToArray();
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(HandicapCalculationService.MinIndex);
    }

    [Fact]
    public void CalculateNewIndex_ClampsToMaxIndex()
    {
        var diffs = Enumerable.Repeat(100.0, 20).ToArray();
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(HandicapCalculationService.MaxIndex);
    }

    [Fact]
    public void CalculateNewIndex_SoftCap_HalvesExcessAbove3()
    {
        // Lowest 8 of 1..20 = 4.5, no cap = 4.5. Low365 = 0.0 means a rise of 4.5,
        // which exceeds soft-cap threshold of 3.0 by 1.5. Soft cap leaves
        // low+3 + (1.5/2) = 0 + 3 + 0.75 = 3.75.
        var diffs = Enumerable.Range(1, 20).Select(i => (double)i).ToArray();
        var result = HandicapCalculationService.CalculateNewIndex(diffs, lowIndexInLast365Days: 0.0);
        result.Should().Be(3.75);
    }

    [Fact]
    public void CalculateNewIndex_HardCap_LimitsRiseTo5()
    {
        // Raw = 10.0 from many high differentials. Low365 = 2.0. Rise = 8.0.
        // Hard cap is low + 5 = 7.0. Soft cap would yield 2 + 3 + (5/2) = 7.5,
        // which exceeds the hard cap — so result is 7.0.
        var diffs = Enumerable.Repeat(10.0, 20).ToArray();
        var result = HandicapCalculationService.CalculateNewIndex(diffs, lowIndexInLast365Days: 2.0);
        result.Should().Be(7.0);
    }

    [Fact]
    public void CalculateNewIndex_NoCap_WhenRiseUnderThreshold()
    {
        // Lowest 1 = 4.0 minus 1 = 3.0 (4 diffs). Low365 = 1.0, rise = 2 <= 3.
        // No cap applied.
        var diffs = new[] { 4.0, 5.0, 6.0, 7.0 };
        var result = HandicapCalculationService.CalculateNewIndex(diffs, lowIndexInLast365Days: 1.0);
        result.Should().Be(3.0);
    }

    [Fact]
    public void CalculateNewIndex_NullLowIgnoresCap()
    {
        // 20 diffs, raw = 4.5. With null low365 (new player), no cap.
        var diffs = Enumerable.Range(1, 20).Select(i => (double)i).ToArray();
        HandicapCalculationService.CalculateNewIndex(diffs, lowIndexInLast365Days: null).Should().Be(4.5);
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
