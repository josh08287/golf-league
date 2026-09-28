using FluentAssertions;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class BingoBangoBongoScoringServiceTests
{
    [Fact]
    public void Tally_AwardsOnePointPerWonHonor()
    {
        var picks = new List<BingoBangoBongoScoringService.HolePick>
        {
            new(1, BbbHonor.FirstOnGreen, WinnerParticipantId: 10),
            new(1, BbbHonor.ClosestOnceOn, WinnerParticipantId: 10),
            new(1, BbbHonor.FirstInHole, WinnerParticipantId: 20),
        };

        var tally = BingoBangoBongoScoringService.Tally(picks);

        tally.Should().ContainSingle(t => t.ParticipantId == 10 && t.Points == 2);
        tally.Should().ContainSingle(t => t.ParticipantId == 20 && t.Points == 1);
    }

    [Fact]
    public void Tally_NullWinner_AwardsNoPoint()
    {
        var picks = new List<BingoBangoBongoScoringService.HolePick>
        {
            new(1, BbbHonor.FirstOnGreen, WinnerParticipantId: null),
            new(1, BbbHonor.ClosestOnceOn, WinnerParticipantId: 10),
        };

        var tally = BingoBangoBongoScoringService.Tally(picks);

        tally.Should().ContainSingle();
        tally.Should().ContainSingle(t => t.ParticipantId == 10 && t.Points == 1);
    }

    [Fact]
    public void Tally_AccumulatesAcrossMultipleHoles()
    {
        var picks = new List<BingoBangoBongoScoringService.HolePick>
        {
            new(1, BbbHonor.FirstOnGreen, 10),
            new(2, BbbHonor.FirstOnGreen, 10),
            new(3, BbbHonor.FirstOnGreen, 10),
        };

        var tally = BingoBangoBongoScoringService.Tally(picks);

        tally.Should().ContainSingle(t => t.ParticipantId == 10 && t.Points == 3);
    }

    [Fact]
    public void Tally_OrdersByPointsDescending()
    {
        var picks = new List<BingoBangoBongoScoringService.HolePick>
        {
            new(1, BbbHonor.FirstOnGreen, 10),
            new(1, BbbHonor.ClosestOnceOn, 20),
            new(1, BbbHonor.FirstInHole, 20),
        };

        var tally = BingoBangoBongoScoringService.Tally(picks);

        tally[0].ParticipantId.Should().Be(20);
        tally[0].Points.Should().Be(2);
        tally[1].ParticipantId.Should().Be(10);
        tally[1].Points.Should().Be(1);
    }
}
