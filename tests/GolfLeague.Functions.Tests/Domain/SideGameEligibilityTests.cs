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
    public void EligibleGames_For9Holes_OnlyReturnsBestBall()
    {
        var eligible = SideGameEligibility.EligibleGames(9);
        eligible.Should().ContainSingle().Which.Should().Be(SideGameType.TwoVTwoBestBall);
    }

    [Fact]
    public void EligibleGames_For18Holes_ReturnsBothGames()
    {
        var eligible = SideGameEligibility.EligibleGames(18);
        eligible.Should().BeEquivalentTo([SideGameType.Nassau, SideGameType.TwoVTwoBestBall]);
    }
}
