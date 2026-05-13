using Xunit;
using GolfLeague.Domain.Services;
using FluentAssertions;

namespace GolfLeague.Tests.Domain;

/// <summary>
/// Additional coverage for the league rolling-average handicap rule.
/// Core happy-path cases live in
/// <c>DomainServicesTests.HandicapCalculationServiceTests</c>.
/// </summary>
public class HandicapCalculationServiceExtendedTests
{
    [Fact]
    public void CalculateNewIndex_ExactlyFiveDiffs_AveragesAll()
    {
        // All five values contributed equally.
        var diffs = new[] { 4.0, 6.0, 8.0, 10.0, 12.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(8.0);
    }

    [Fact]
    public void CalculateNewIndex_SixDiffs_OnlyFirstFiveUsed()
    {
        // Newest-first; sixth value (100) must be ignored.
        var diffs = new[] { 4.0, 6.0, 8.0, 10.0, 12.0, 100.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(8.0);
    }

    [Fact]
    public void CalculateNewIndex_SingleDiff_ReturnsThatValue()
    {
        HandicapCalculationService.CalculateNewIndex(new[] { 15.0 }).Should().Be(15.0);
    }

    [Fact]
    public void CalculateNewIndex_HighDifferentials_NoUpperCap()
    {
        // League rule has no hard cap — large differentials average normally.
        var diffs = new[] { 30.0, 30.0, 30.0, 30.0, 30.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(30.0);
    }

    [Fact]
    public void CalculateNewIndex_NegativeDifferentials_AveragesCorrectly()
    {
        // Scratch or plus-handicap scenario.
        var diffs = new[] { -2.0, -1.0, 0.0, 1.0, 2.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(0.0);
    }

    [Fact]
    public void CalculateNewIndex_RoundingUsesToEven()
    {
        // (5+6+8+10)/4 = 7.25 — ToEven rounds to 7.2 (digit before 5 is even).
        var diffs = new[] { 5.0, 6.0, 8.0, 10.0 };
        HandicapCalculationService.CalculateNewIndex(diffs).Should().Be(7.2);
    }
}
