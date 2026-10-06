using FluentAssertions;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
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

    private static Round MakeFinalizedSeasonRound(int id, int seasonId = 1, int? halfId = null) => new()
    {
        Id = id,
        SeasonId = seasonId,
        HalfId = halfId,
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
        public Mock<ILeagueSettingRepository> Settings { get; } = new();
        public Mock<ILeagueContext> LeagueContext { get; } = new();

        public Mocks()
        {
            LeagueContext.Setup(c => c.LeagueId).Returns(1);
            // No override configured — handler falls back to the documented
            // default of dropping 1 round per half, same as flight standings.
            Settings.Setup(s => s.GetAsync(1, KnownSettings.StandingsDropCount, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LeagueSetting?)null);
        }

        public GetLeagueChampionshipQueryHandler BuildSut() => new(RoundRepo.Object, Settings.Object, LeagueContext.Object);
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
    public async Task Handle_DropsWorstNRoundsPerHalf_PerLeagueStandingsDropCountSetting()
    {
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, KnownSettings.StandingsDropCount, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = KnownSettings.StandingsDropCount, Value = "2" });

        var round = MakeTournamentRound();
        m.RoundRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(round);

        // Half 1 (4 rounds): drop the worst 2 (points 5, 10) -> counts 20 + 15 = 35.
        // Half 2 (2 rounds): drop count is clamped to roundsInHalf.Count - 1 = 1 -> drops 8, counts 12.
        // Player's season total = 35 + 12 = 47.
        m.RoundRepo.Setup(r => r.GetBySeasonAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Round>)[
                MakeFinalizedSeasonRound(10, halfId: 1),
                MakeFinalizedSeasonRound(11, halfId: 1),
                MakeFinalizedSeasonRound(12, halfId: 1),
                MakeFinalizedSeasonRound(13, halfId: 1),
                MakeFinalizedSeasonRound(20, halfId: 2),
                MakeFinalizedSeasonRound(21, halfId: 2),
            ]);

        m.RoundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeSeasonParticipant(1, 10, netPoints: 20, grossPoints: 0),
                MakeSeasonParticipant(1, 11, netPoints: 15, grossPoints: 0),
                MakeSeasonParticipant(1, 12, netPoints: 10, grossPoints: 0),
                MakeSeasonParticipant(1, 13, netPoints: 5, grossPoints: 0),
                MakeSeasonParticipant(1, 20, netPoints: 12, grossPoints: 0),
                MakeSeasonParticipant(1, 21, netPoints: 8, grossPoints: 0),
                // Player 2 exists only so the field has more than one seed.
                MakeSeasonParticipant(2, 10, netPoints: 0, grossPoints: 0),
            ]);

        m.RoundRepo.Setup(r => r.GetParticipantsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RoundParticipant>)[
                MakeRoundEntry(1, 1, netStrokes: 80, grossStrokes: 90),
                MakeRoundEntry(2, 1, netStrokes: 80, grossStrokes: 90),
            ]);

        var result = await m.BuildSut().Handle(new GetLeagueChampionshipQuery(1, UseGrossPoints: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Standings.Single(s => s.PlayerId == 1).SeasonPoints.Should().Be(47);
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
