using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class WolfScoringServiceTests
{
    private static readonly List<int> FourPlayers = [1, 2, 3, 4];

    [Fact]
    public void ScoreHole_WolfWithPartner_WinsWithNormalPoints()
    {
        var pick = new WolfScoringService.HolePick(HoleNumber: 1, WolfParticipantId: 1, IsLoneWolf: false, PartnerParticipantId: 2);
        var strokes = new Dictionary<int, int> { [1] = 4, [2] = 5, [3] = 5, [4] = 6 };

        var outcome = WolfScoringService.ScoreHole(pick, strokes, FourPlayers);

        outcome.Winner.Should().Be(BestBallScoringService.HoleWinner.TeamA);
        outcome.PointsAwarded.Should().Be(WolfScoringService.NormalHolePoints);
        outcome.WolfSideParticipantIds.Should().BeEquivalentTo([1, 2]);
        outcome.OtherSideParticipantIds.Should().BeEquivalentTo([3, 4]);
    }

    [Fact]
    public void ScoreHole_LoneWolfWins_AwardsDoublePoints()
    {
        var pick = new WolfScoringService.HolePick(1, WolfParticipantId: 1, IsLoneWolf: true, PartnerParticipantId: null);
        var strokes = new Dictionary<int, int> { [1] = 3, [2] = 5, [3] = 5, [4] = 6 };

        var outcome = WolfScoringService.ScoreHole(pick, strokes, FourPlayers);

        outcome.Winner.Should().Be(BestBallScoringService.HoleWinner.TeamA);
        outcome.PointsAwarded.Should().Be(WolfScoringService.LoneWolfHolePoints);
        outcome.WolfSideParticipantIds.Should().BeEquivalentTo([1]);
        outcome.OtherSideParticipantIds.Should().BeEquivalentTo([2, 3, 4]);
    }

    [Fact]
    public void ScoreHole_LoneWolfLoses_OtherSideAwardedDoublePoints()
    {
        var pick = new WolfScoringService.HolePick(1, WolfParticipantId: 1, IsLoneWolf: true, PartnerParticipantId: null);
        var strokes = new Dictionary<int, int> { [1] = 6, [2] = 5, [3] = 5, [4] = 6 };

        var outcome = WolfScoringService.ScoreHole(pick, strokes, FourPlayers);

        outcome.Winner.Should().Be(BestBallScoringService.HoleWinner.TeamB);
        outcome.PointsAwarded.Should().Be(WolfScoringService.LoneWolfHolePoints);
    }

    [Fact]
    public void ScoreHole_Halved_AwardsNoPoints()
    {
        var pick = new WolfScoringService.HolePick(1, WolfParticipantId: 1, IsLoneWolf: false, PartnerParticipantId: 2);
        var strokes = new Dictionary<int, int> { [1] = 4, [2] = 5, [3] = 4, [4] = 6 };

        var outcome = WolfScoringService.ScoreHole(pick, strokes, FourPlayers);

        outcome.Winner.Should().Be(BestBallScoringService.HoleWinner.Halved);
        outcome.PointsAwarded.Should().Be(0);
    }

    [Fact]
    public void ScoreHole_MissingScores_HalvesWithNoPoints()
    {
        var pick = new WolfScoringService.HolePick(1, WolfParticipantId: 1, IsLoneWolf: true, PartnerParticipantId: null);
        var strokes = new Dictionary<int, int> { [1] = 4 }; // other side has no scores recorded yet

        var outcome = WolfScoringService.ScoreHole(pick, strokes, FourPlayers);

        outcome.PointsAwarded.Should().Be(0);
    }

    [Fact]
    public void Tally_SplitsPointsAcrossWinningPartnerSide()
    {
        var outcomes = new List<WolfScoringService.HoleOutcome>
        {
            new(1, BestBallScoringService.HoleWinner.TeamA, PointsAwarded: 1, WolfSideParticipantIds: [1, 2], OtherSideParticipantIds: [3, 4]),
        };

        var tally = WolfScoringService.Tally(outcomes);

        tally.Should().Contain(t => t.ParticipantId == 1 && t.Points == 1);
        tally.Should().Contain(t => t.ParticipantId == 2 && t.Points == 1);
        tally.Should().NotContain(t => t.ParticipantId == 3);
    }

    [Fact]
    public void Tally_LoneWolfWin_KeepsAllPointsAlone()
    {
        var outcomes = new List<WolfScoringService.HoleOutcome>
        {
            new(1, BestBallScoringService.HoleWinner.TeamA, PointsAwarded: 2, WolfSideParticipantIds: [1], OtherSideParticipantIds: [2, 3, 4]),
        };

        var tally = WolfScoringService.Tally(outcomes);

        tally.Should().ContainSingle(t => t.ParticipantId == 1 && t.Points == 2);
    }

    [Fact]
    public void Tally_AccumulatesAcrossHoles()
    {
        var outcomes = new List<WolfScoringService.HoleOutcome>
        {
            new(1, BestBallScoringService.HoleWinner.TeamA, 1, [1, 2], [3, 4]),
            new(2, BestBallScoringService.HoleWinner.TeamA, 2, [1], [2, 3, 4]),
        };

        var tally = WolfScoringService.Tally(outcomes);

        tally.First(t => t.ParticipantId == 1).Points.Should().Be(3);
        tally.First(t => t.ParticipantId == 2).Points.Should().Be(1);
    }
}
