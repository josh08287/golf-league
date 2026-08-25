using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class MatchPlayScoringServiceTests
{
    [Fact]
    public void HolePoints_LowerNetStrokesWins()
    {
        var (playerPoints, opponentPoints) = MatchPlayScoringService.HolePoints(playerNetStrokes: 3, opponentNetStrokes: 4);
        playerPoints.Should().Be(2);
        opponentPoints.Should().Be(0);
    }

    [Fact]
    public void HolePoints_HigherNetStrokesLoses()
    {
        var (playerPoints, opponentPoints) = MatchPlayScoringService.HolePoints(playerNetStrokes: 5, opponentNetStrokes: 4);
        playerPoints.Should().Be(0);
        opponentPoints.Should().Be(2);
    }

    [Fact]
    public void HolePoints_EqualNetStrokesHalves()
    {
        var (playerPoints, opponentPoints) = MatchPlayScoringService.HolePoints(playerNetStrokes: 4, opponentNetStrokes: 4);
        playerPoints.Should().Be(1);
        opponentPoints.Should().Be(1);
    }

    [Fact]
    public void AgainstCardHolePoints_NetBelowPar_Wins()
    {
        MatchPlayScoringService.AgainstCardHolePoints(netStrokes: 3, par: 4).Should().Be(2);
    }

    [Fact]
    public void AgainstCardHolePoints_NetAbovePar_Loses()
    {
        MatchPlayScoringService.AgainstCardHolePoints(netStrokes: 5, par: 4).Should().Be(0);
    }

    [Fact]
    public void AgainstCardHolePoints_NetEqualsPar_Halves()
    {
        MatchPlayScoringService.AgainstCardHolePoints(netStrokes: 4, par: 4).Should().Be(1);
    }

    [Fact]
    public void MatchBonus_MoreHolesWon_AwardsBonusToPlayer()
    {
        var (playerBonus, opponentBonus) = MatchPlayScoringService.MatchBonus(playerHolesWon: 5, opponentHolesWon: 3);
        playerBonus.Should().Be(4);
        opponentBonus.Should().Be(0);
    }

    [Fact]
    public void MatchBonus_FewerHolesWon_AwardsBonusToOpponent()
    {
        var (playerBonus, opponentBonus) = MatchPlayScoringService.MatchBonus(playerHolesWon: 2, opponentHolesWon: 6);
        playerBonus.Should().Be(0);
        opponentBonus.Should().Be(4);
    }

    [Fact]
    public void MatchBonus_TiedHolesWon_NoBonus()
    {
        var (playerBonus, opponentBonus) = MatchPlayScoringService.MatchBonus(playerHolesWon: 4, opponentHolesWon: 4);
        playerBonus.Should().Be(0);
        opponentBonus.Should().Be(0);
    }

    [Fact]
    public void MatchBonus_BothZeroHolesWon_NoBonus()
    {
        var (playerBonus, opponentBonus) = MatchPlayScoringService.MatchBonus(playerHolesWon: 0, opponentHolesWon: 0);
        playerBonus.Should().Be(0);
        opponentBonus.Should().Be(0);
    }
}
