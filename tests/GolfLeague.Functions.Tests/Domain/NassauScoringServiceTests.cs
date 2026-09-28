using FluentAssertions;
using GolfLeague.Domain.Services;
using Xunit;

namespace GolfLeague.Tests.Domain;

public class NassauScoringServiceTests
{
    [Fact]
    public void ScoreSegment_SideAWinsMoreHoles_LeaderIsSideA()
    {
        var holes = new List<NassauScoringService.HoleResult>
        {
            new(1, SideAStrokes: 4, SideBStrokes: 5), // A wins
            new(2, SideAStrokes: 5, SideBStrokes: 5), // halved
            new(3, SideAStrokes: 3, SideBStrokes: 4), // A wins
        };

        var result = NassauScoringService.ScoreSegment(holes, totalHolesInSegment: 9);

        result.Leader.Should().Be(NassauScoringService.Leader.SideA);
        result.HolesUp.Should().Be(2);
        result.HolesRemaining.Should().Be(6);
    }

    [Fact]
    public void ScoreSegment_TiedHoles_LeaderIsTied()
    {
        var holes = new List<NassauScoringService.HoleResult>
        {
            new(1, 4, 5),
            new(2, 5, 4),
        };

        var result = NassauScoringService.ScoreSegment(holes, totalHolesInSegment: 9);

        result.Leader.Should().Be(NassauScoringService.Leader.Tied);
        result.HolesUp.Should().Be(0);
    }

    [Fact]
    public void Score_SplitsFrontBackAndOverallCorrectly()
    {
        var holes = new List<NassauScoringService.HoleResult>();
        // Front 9: A wins every hole.
        for (int h = 1; h <= 9; h++)
            holes.Add(new NassauScoringService.HoleResult(h, SideAStrokes: 3, SideBStrokes: 5));
        // Back 9: B wins every hole.
        for (int h = 10; h <= 18; h++)
            holes.Add(new NassauScoringService.HoleResult(h, SideAStrokes: 5, SideBStrokes: 3));

        var result = NassauScoringService.Score(holes);

        result.Front.Leader.Should().Be(NassauScoringService.Leader.SideA);
        result.Front.HolesUp.Should().Be(9);
        result.Back.Leader.Should().Be(NassauScoringService.Leader.SideB);
        result.Back.HolesUp.Should().Be(9);
        // Overall: 9 holes to A, 9 holes to B -> tied.
        result.Overall.Leader.Should().Be(NassauScoringService.Leader.Tied);
    }

    [Fact]
    public void Score_PartiallyPlayedRound_ReportsHolesRemaining()
    {
        var holes = new List<NassauScoringService.HoleResult>
        {
            new(1, 4, 5),
            new(2, 4, 5),
            new(3, 4, 5),
        };

        var result = NassauScoringService.Score(holes);

        result.Front.HolesRemaining.Should().Be(6);
        result.Back.HolesRemaining.Should().Be(9);
        result.Overall.HolesRemaining.Should().Be(15);
    }

    [Fact]
    public void BestBallStrokes_ReturnsLowestStroke()
    {
        NassauScoringService.BestBallStrokes([5, 3, 7]).Should().Be(3);
    }
}
