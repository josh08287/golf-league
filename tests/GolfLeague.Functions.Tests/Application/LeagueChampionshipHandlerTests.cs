using FluentAssertions;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// GetLeagueChampionshipQueryHandler owns the season-long seeding (by gross/net
/// Stableford points), the proportional starting-stroke-advantage math, and the
/// substitute-exclusion rule for the League Championship tab — none of which had
/// test coverage.
/// </summary>
public class LeagueChampionshipHandlerTests
{
    private static Player MakePlayer(int id, string name) => new() { Id = id, FirstName = name, LastName = "P" };

    private static Round MakeTournamentRound(int id = 1, int seasonId = 1) => new()
    {
        Id = id,
        SeasonId = seasonId,
        RoundType = RoundType.Tournament,
        RoundDate = new DateOnly(2026, 9, 1),
        CourseId = 1,
    };

    private static Round MakeFinalizedSeasonRound(int id, int seasonId = 1) => new()
    {
        Id = id,
        SeasonId = seasonId,
        Status = RoundStatus.Finalized,
        RoundDate = new DateOnly(2026, 6, id),
        CourseId = 1,
    };

    private static RoundParticipant MakeSeasonParticipant(
        int playerId, int roundId, int netPoints, int grossPoints, bool isSubstitute = false, bool isWithdrawn = false) => new()
    {
        Id = playerId * 1000 + roundId,
        PlayerId = playerId,
        Player = MakePlayer(playerId, $"Player{playerId}"),
        RoundId = roundId,
        TotalNetStablefordPoints = netPoints,
        TotalGrossStablefordPoints = grossPoints,
        IsSubstitute = isSubstitute,
        IsWithdrawn = isWithdrawn,
    };

    private static RoundParticipant MakeRoundEntry(
        int playerId, int roundId, int? netStrokes, int? grossStrokes, bool isSubstitute = false, bool isWithdrawn = false) => new()
    {
        Id = playerId * 1000 + roundId,
        PlayerId = playerId,
        Player = MakePlayer(playerId, $"Player{playerId}"),
        RoundId = roundId,
        TotalNetStrokes = netStrokes,
        TotalGrossStrokes = grossStrokes,
        IsSubstitute = isSubstitute,
        IsWithdrawn = isWithdrawn,
    };

    private sealed class Mocks
    {
        public Mock<IRoundRepository> RoundRepo { get; } = new();

        public GetLeagueChampionshipQueryHandler BuildSut() => new(RoundRepo.Object);
    }

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Round?)null);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenRoundIsNotTournament_ReturnsFail()
    {
        var m = new Mocks();
        var round = MakeTournamentRound();
        round.RoundType = RoundType.EighteenHole;
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SeedsByDescendingSeasonPoints_AndAppliesProportionalStrokeAdvantage()
    {
        var m = new Mocks();
        var round = MakeTournamentRound();
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.RoundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Round>)[MakeFinalizedSeasonRound(10), MakeFinalizedSeasonRound(11)]);

        // Season standings: Player1 = 60 net pts, Player2 = 40, Player3 = 20 (last place).
        m.RoundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.Is<IEnumerable<int>>(ids => ids.Contains(10) && ids.Contains(11)), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeSeasonParticipant(1, 10, netPoints: 30, grossPoints: 0),
                MakeSeasonParticipant(1, 11, netPoints: 30, grossPoints: 0),
                MakeSeasonParticipant(2, 10, netPoints: 20, grossPoints: 0),
                MakeSeasonParticipant(2, 11, netPoints: 20, grossPoints: 0),
                MakeSeasonParticipant(3, 10, netPoints: 10, grossPoints: 0),
                MakeSeasonParticipant(3, 11, netPoints: 10, grossPoints: 0),
            ]);

        // This round's scores.
        m.RoundRepo.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeRoundEntry(1, 1, netStrokes: 80, grossStrokes: 90),
                MakeRoundEntry(2, 1, netStrokes: 75, grossStrokes: 85),
                MakeRoundEntry(3, 1, netStrokes: 75, grossStrokes: 85),
            ]);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1, UseGrossPoints: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value!;
        dto.Standings.Should().HaveCount(3);

        var player1 = dto.Standings.Single(s => s.PlayerId == 1);
        var player2 = dto.Standings.Single(s => s.PlayerId == 2);
        var player3 = dto.Standings.Single(s => s.PlayerId == 3);

        // Seeds assigned by descending season points: Player1 is seed 1 (60 pts), Player2 seed 2 (40), Player3 seed 3 (20).
        player1.Seed.Should().Be(1);
        player2.Seed.Should().Be(2);
        player3.Seed.Should().Be(3);

        // Top seed gets the max advantage, last-place seed gets zero, proportional in between.
        player1.StartingStrokeAdvantage.Should().Be(10);
        player3.StartingStrokeAdvantage.Should().Be(0);
        player2.StartingStrokeAdvantage.Should().BeInRange(1, 9);

        // Adjusted score = round strokes - advantage. Player1: 80 - 10 = 70.
        // Player2: 75 - advantage. Player3: 75 - 0 = 75.
        player1.AdjustedScore.Should().Be(70);
        player3.AdjustedScore.Should().Be(75);

        // Player1 ends up leading despite a higher raw score, because of the stroke advantage.
        dto.Standings.OrderBy(s => s.Rank).First().PlayerId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExcludesSubstitutesFromSeasonStandingsAndFromTheRoundItself()
    {
        var m = new Mocks();
        var round = MakeTournamentRound();
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.RoundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Round>)[MakeFinalizedSeasonRound(10)]);

        m.RoundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeSeasonParticipant(1, 10, netPoints: 30, grossPoints: 0),
                MakeSeasonParticipant(2, 10, netPoints: 99, grossPoints: 0, isSubstitute: true),
            ]);

        m.RoundRepo.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeRoundEntry(1, 1, netStrokes: 80, grossStrokes: 90),
                MakeRoundEntry(2, 1, netStrokes: 70, grossStrokes: 80, isSubstitute: true),
            ]);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Standings.Should().ContainSingle(s => s.PlayerId == 1);
        result.Value!.Standings.Should().NotContain(s => s.PlayerId == 2);
    }

    [Fact]
    public async Task Handle_WhenNoEligibleParticipantsInRound_ReturnsEmptyStandings()
    {
        var m = new Mocks();
        var round = MakeTournamentRound();
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.RoundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Round>)[]);
        m.RoundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[]);
        m.RoundRepo.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[]);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Standings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PlayersWithNoScoreYetSortToBottomBySeed()
    {
        var m = new Mocks();
        var round = MakeTournamentRound();
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);
        m.RoundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Round>)[MakeFinalizedSeasonRound(10)]);
        m.RoundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeSeasonParticipant(1, 10, netPoints: 30, grossPoints: 0),
                MakeSeasonParticipant(2, 10, netPoints: 10, grossPoints: 0),
            ]);
        m.RoundRepo.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeRoundEntry(1, 1, netStrokes: null, grossStrokes: null),
                MakeRoundEntry(2, 1, netStrokes: 80, grossStrokes: 90),
            ]);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var ordered = result.Value!.Standings.OrderBy(s => s.Rank).ToList();
        ordered.First().PlayerId.Should().Be(2); // has a score
        ordered.Last().PlayerId.Should().Be(1); // no score yet, sorts last despite higher seed
    }
}
