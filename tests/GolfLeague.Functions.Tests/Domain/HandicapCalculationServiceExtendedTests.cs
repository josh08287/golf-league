using Xunit;
using GolfLeague.Domain.Services;
using FluentAssertions;

namespace GolfLeague.Tests.Domain;

/// <summary>
/// Coverage for the WHS rule 5.2a "score differentials used" lookup table,
/// the soft / hard cap from rule 5.8, and edge cases. The core happy-path
/// cases live in <c>DomainServicesTests.HandicapCalculationServiceTests</c>.
/// </summary>
public class HandicapCalculationServiceExtendedTests
{
    [Theory]
    [InlineData(0, 0, 0.0)]
    [InlineData(1, 1, -2.0)]
    [InlineData(2, 1, -2.0)]
    [InlineData(3, 1, -2.0)]
    [InlineData(4, 1, -1.0)]
    [InlineData(5, 1, 0.0)]
    [InlineData(6, 2, -1.0)]
    [InlineData(7, 2, 0.0)]
    [InlineData(8, 2, 0.0)]
    [InlineData(9, 3, 0.0)]
    [InlineData(11, 3, 0.0)]
    [InlineData(12, 4, 0.0)]
    [InlineData(14, 4, 0.0)]
    [InlineData(15, 5, 0.0)]
    [InlineData(16, 5, 0.0)]
    [InlineData(17, 6, 0.0)]
    [InlineData(18, 6, 0.0)]
    [InlineData(19, 7, 0.0)]
    [InlineData(20, 8, 0.0)]
    public void WhsSelection_MatchesWhsTable(int diffCount, int expectedLowestCount, double expectedAdjustment)
    {
        var (lowest, adj) = HandicapCalculationService.WhsSelection(diffCount);
        lowest.Should().Be(expectedLowestCount);
        adj.Should().Be(expectedAdjustment);
    }

    [Fact]
    public void ApplyCaps_NoLowIndex_ReturnsRawUnchanged()
    {
        HandicapCalculationService.ApplyCaps(rawIndex: 25.0, lowIndexInLast365Days: null).Should().Be(25.0);
    }

    [Fact]
    public void ApplyCaps_RiseAtOrBelowThreshold_ReturnsRawUnchanged()
    {
        // rise of exactly 3.0 is at the soft-cap threshold — not exceeded.
        HandicapCalculationService.ApplyCaps(rawIndex: 13.0, lowIndexInLast365Days: 10.0).Should().Be(13.0);
    }

    [Fact]
    public void ApplyCaps_SoftCap_HalvesExcess()
    {
        // rise = 4.0, excess = 1.0; soft-capped = low + 3 + 0.5 = 10 + 3 + 0.5 = 13.5
        HandicapCalculationService.ApplyCaps(rawIndex: 14.0, lowIndexInLast365Days: 10.0).Should().Be(13.5);
    }

    [Fact]
    public void ApplyCaps_HardCap_TakesOverAtRiseAboveFive()
    {
        // raw 20, low 10, rise = 10. Soft: 10+3+(7/2)=16.5. Hard ceiling: 10+5=15.
        // Hard wins.
        HandicapCalculationService.ApplyCaps(rawIndex: 20.0, lowIndexInLast365Days: 10.0).Should().Be(15.0);
    }

    [Fact]
    public void ApplyCaps_NegativeLow_AppliesSoftCap()
    {
        // Plus-handicap (-2) golfer; raw +4 means rise = 6, excess over threshold = 3.
        // Soft = -2 + 3 + (3/2) = 2.5. Hard ceiling = -2 + 5 = 3.0. Soft wins.
        HandicapCalculationService.ApplyCaps(rawIndex: 4.0, lowIndexInLast365Days: -2.0).Should().Be(2.5);
    }

    [Fact]
    public void ApplyCaps_NegativeLow_AppliesHardCap()
    {
        // -2 golfer; raw +20, rise=22, excess=19; soft = -2+3+(19/2)=10.5
        // Hard ceiling = -2 + 5 = 3.0. Hard wins.
        HandicapCalculationService.ApplyCaps(rawIndex: 20.0, lowIndexInLast365Days: -2.0).Should().Be(3.0);
    }

    [Fact]
    public void CalculateNewIndex_HandlesUnsortedInput()
    {
        // Order shouldn't matter — method sorts internally.
        var ordered = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
        var shuffled = new[] { 5.0, 1.0, 3.0, 2.0, 4.0 };
        HandicapCalculationService.CalculateNewIndex(ordered)
            .Should().Be(HandicapCalculationService.CalculateNewIndex(shuffled));
    }
}
