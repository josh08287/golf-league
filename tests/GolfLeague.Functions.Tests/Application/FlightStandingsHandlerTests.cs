using FluentAssertions;
using GolfLeague.Application.Flights.Queries;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

/// <summary>
/// GetFlightStandingsQueryHandler owns the league's drop-N-lowest-rounds
/// standings math, skipped-week-safe averaging, and rank-based Position
/// assignment — none of which had test coverage.
/// </summary>
public class FlightStandingsHandlerTests
{
    private static Flight MakeFlight(int id = 1) => new() { Id = id, Name = "A" };

    private static Player MakePlayer(int id, string name) => new() { Id = id, FirstName = name, LastName = "P" };

    private static Round MakeRound(int weekNumber) => new()
    {
        Id = weekNumber,
        WeekNumber = weekNumber,
        RoundDate = new DateOnly(2026, 6, weekNumber),
        CourseId = 1,
    };

    private static RoundParticipant MakeRoundResult(
        int playerId, int weekNumber, int netPoints, int grossPoints, bool skipped = false, bool withdrawn = false,
        int? netStrokes = null, int? grossStrokes = null) => new()
    {
        Id = playerId * 100 + weekNumber,
        PlayerId = playerId,
        RoundId = weekNumber,
        Round = MakeRound(weekNumber),
        TotalNetStablefordPoints = netPoints,
        TotalGrossStablefordPoints = grossPoints,
        TotalNetStrokes = netStrokes,
        TotalGrossStrokes = grossStrokes,
        SkippedWeek = skipped,
        IsWithdrawn = withdrawn,
    };

    private sealed class Mocks
    {
        public Mock<IFlightRepository> FlightRepo { get; } = new();
        public Mock<IHandicapRepository> HandicapRepo { get; } = new();
        public Mock<IPlayerRepository> PlayerRepo { get; } = new();
        public Mock<ILeagueSettingRepository> Settings { get; } = new();
        public Mock<ILeagueContext> LeagueContext { get; } = new();

        public Mocks()
        {
            FlightRepo.Setup(f => f.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(MakeFlight());
            LeagueContext.Setup(c => c.LeagueId).Returns(1);
            Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
                .ReturnsAsync((LeagueSetting?)null); // defaults to drop 1
            HandicapRepo.Setup(h => h.GetCurrentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Handicap?)null);
            PlayerRepo.Setup(p => p.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => MakePlayer(id, $"Player{id}"));
        }

        public GetFlightStandingsQueryHandler BuildSut() =>
            new(FlightRepo.Object, HandicapRepo.Object, PlayerRepo.Object, Settings.Object, LeagueContext.Object);
    }

    [Fact]
    public async Task Handle_WhenFlightNotFound_ReturnsFail()
    {
        var m = new Mocks();
        m.FlightRepo.Setup(f => f.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Flight?)null);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DefaultDropCount_DropsOneLowestScoringRound()
    {
        // Player has 3 rounds: 10, 20, 30 points. Default drop = 1 (the 10).
        // Total should be 20 + 30 = 50; average over 2 counted rounds = 25.
        var m = new Mocks();
        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 10, grossPoints: 10),
            MakeRoundResult(1, 2, netPoints: 20, grossPoints: 20),
            MakeRoundResult(1, 3, netPoints: 30, grossPoints: 30),
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        var standing = result.Value!.Single();
        standing.RoundsPlayed.Should().Be(3);
        standing.TotalPoints.Should().Be(50);
        standing.AveragePoints.Should().Be(25.0);
        standing.RoundScores.Single(rs => rs.WeekNumber == 1).IsDropped.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CustomDropCount_DropsConfiguredNumberOfLowestRounds()
    {
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "2" });

        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 5, grossPoints: 5),
            MakeRoundResult(1, 2, netPoints: 10, grossPoints: 10),
            MakeRoundResult(1, 3, netPoints: 30, grossPoints: 30),
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        var standing = result.Value!.Single();
        standing.TotalPoints.Should().Be(30, "the two lowest rounds (5, 10) are dropped");
    }

    [Fact]
    public async Task Handle_DropCountNeverDropsAllRounds_AtLeastOneAlwaysCounts()
    {
        // Only 1 round played, drop count configured at 1 — must not drop it
        // to zero, or the player would have zero counting rounds.
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "1" });

        var results = new List<RoundParticipant> { MakeRoundResult(1, 1, netPoints: 15, grossPoints: 15) };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        var standing = result.Value!.Single();
        standing.TotalPoints.Should().Be(15, "at least one round must always count");
        standing.RoundScores.Single().IsDropped.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_InvalidDropCountSetting_FallsBackToDefaultOfOne()
    {
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "not-a-number" });

        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 10, grossPoints: 10),
            MakeRoundResult(1, 2, netPoints: 20, grossPoints: 20),
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        var standing = result.Value!.Single();
        standing.TotalPoints.Should().Be(20, "falls back to dropping 1 round when the setting is unparseable");
    }

    [Fact]
    public async Task Handle_SkippedWeek_CountsTowardDropEligibilityButNotAverage()
    {
        // A skipped week scores 0 points. If it's NOT dropped, it must still
        // reduce the total but must not be included in the averaging denominator
        // (skipped weeks shouldn't deflate the "average points per round played").
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "0" });

        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 0, grossPoints: 0, skipped: true),
            MakeRoundResult(1, 2, netPoints: 20, grossPoints: 20),
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        var standing = result.Value!.Single();
        standing.TotalPoints.Should().Be(20, "nothing dropped, so total includes the skipped week's 0 points");
        standing.AveragePoints.Should().Be(20.0, "average is computed only over scored (non-skipped) rounds");
    }

    [Fact]
    public async Task Handle_UseGrossPoints_UsesGrossInsteadOfNetFigures()
    {
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "0" });

        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 10, grossPoints: 25, netStrokes: 90, grossStrokes: 95),
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1, UseGrossPoints: true), CancellationToken.None);

        var standing = result.Value!.Single();
        standing.TotalPoints.Should().Be(25);
        standing.AverageScore.Should().Be(95.0);
    }

    [Fact]
    public async Task Handle_WithdrawnParticipant_ExcludedEntirely()
    {
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "0" });

        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 10, grossPoints: 10),
            MakeRoundResult(2, 1, netPoints: 999, grossPoints: 999, withdrawn: true),
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        var result = await m.BuildSut().Handle(new GetFlightStandingsQuery(1, 1), CancellationToken.None);

        result.Value!.Should().ContainSingle(s => s.PlayerId == 1);
        result.Value!.Should().NotContain(s => s.PlayerId == 2);
    }

    [Fact]
    public async Task Handle_PositionRanksByTotalPointsThenAveragePoints_AssignedBeforeSort()
    {
        var m = new Mocks();
        m.Settings.Setup(s => s.GetAsync(1, "standings_drop_count", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueSetting { LeagueId = 1, Key = "standings_drop_count", Value = "0" });

        var results = new List<RoundParticipant>
        {
            MakeRoundResult(1, 1, netPoints: 10, grossPoints: 10), // player 1: 10 total
            MakeRoundResult(2, 1, netPoints: 30, grossPoints: 30), // player 2: 30 total (rank 1)
            MakeRoundResult(3, 1, netPoints: 20, grossPoints: 20), // player 3: 20 total (rank 2)
        };
        m.FlightRepo.Setup(f => f.GetStandingsAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(results);

        // Sort request re-orders by player name, but Position must still reflect rank-by-points.
        var result = await m.BuildSut().Handle(
            new GetFlightStandingsQuery(1, 1, Sort: new GolfLeague.Application.Common.SortRequest("player", GolfLeague.Application.Common.SortDirection.Ascending)),
            CancellationToken.None);

        var byPlayer = result.Value!.ToDictionary(s => s.PlayerId);
        byPlayer[2].Position.Should().Be(1);
        byPlayer[3].Position.Should().Be(2);
        byPlayer[1].Position.Should().Be(3);
    }
}
