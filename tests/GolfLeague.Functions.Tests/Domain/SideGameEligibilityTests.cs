using FluentAssertions;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class SideGameEligibilityTests
{
    [Fact]
    public void Nassau_IsValidFor18Holes()
    {
        SideGameEligibility.IsValidFor(SideGameType.Nassau, 18).Should().BeTrue();
    }

    [Fact]
    public void Nassau_IsNotValidFor9Holes()
    {
        SideGameEligibility.IsValidFor(SideGameType.Nassau, 9).Should().BeFalse();
    }

    [Fact]
    public void TwoVTwoBestBall_IsValidFor9And18Holes()
    {
        SideGameEligibility.IsValidFor(SideGameType.TwoVTwoBestBall, 9).Should().BeTrue();
        SideGameEligibility.IsValidFor(SideGameType.TwoVTwoBestBall, 18).Should().BeTrue();
    }

    [Fact]
    public void BingoBangoBongo_IsValidFor9And18Holes()
    {
        SideGameEligibility.IsValidFor(SideGameType.BingoBangoBongo, 9).Should().BeTrue();
        SideGameEligibility.IsValidFor(SideGameType.BingoBangoBongo, 18).Should().BeTrue();
    }

    [Fact]
    public void Wolf_IsValidFor9And18Holes()
    {
        SideGameEligibility.IsValidFor(SideGameType.Wolf, 9).Should().BeTrue();
        SideGameEligibility.IsValidFor(SideGameType.Wolf, 18).Should().BeTrue();
    }

    [Fact]
    public void EligibleGames_For9Holes_ExcludesNassauOnly()
    {
        var eligible = SideGameEligibility.EligibleGames(9);
        eligible.Should().BeEquivalentTo([SideGameType.TwoVTwoBestBall, SideGameType.BingoBangoBongo, SideGameType.Wolf]);
    }

    [Fact]
    public void EligibleGames_For18Holes_ReturnsAllGames()
    {
        var eligible = SideGameEligibility.EligibleGames(18);
        eligible.Should().BeEquivalentTo([SideGameType.Nassau, SideGameType.TwoVTwoBestBall, SideGameType.BingoBangoBongo, SideGameType.Wolf]);
    }
}
