using FluentAssertions;
using GolfLeague.Application.Statistics.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class GetLeagueLeaderboardsQueryHandlerTests
{
    private readonly Mock<IRoundRepository> _roundRepo;
    private readonly Mock<IPlayerRepository> _playerRepo;
    private readonly GetLeagueLeaderboardsQueryHandler _handler;

    public GetLeagueLeaderboardsQueryHandlerTests()
    {
        _roundRepo = new Mock<IRoundRepository>();
        _playerRepo = new Mock<IPlayerRepository>();
        _roundRepo.Setup(r => r.GetClosestToPinWinnersAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundClosestToPin>());
        _handler = new GetLeagueLeaderboardsQueryHandler(_roundRepo.Object, _playerRepo.Object);
    }

    [Fact]
    public async Task Handle_AverageNetScore_CalculatesCorrectlyFromTotalNetStrokes()
    {
        // Setup: Two rounds with different handicaps for the same player
        // The net score should use the handicap AT THE TIME OF EACH ROUND
        
        var round1 = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1
        };

        var round2 = new Round
        {
            Id = 2,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 8),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 2
        };

        // Player 1: In round 1, had handicap 5, scored net 40
        // In round 2, had handicap 3, scored net 42
        // Expected average net = (40 + 42) / 2 = 41.0
        var participant1Round1 = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 10.0, // 18-hole index at time of round
            CourseHandicap = 5,   // 9-hole course handicap (this is what gets applied)
            TotalGrossStrokes = 45,
            TotalNetStrokes = 40, // 45 - 5 strokes = 40
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 1, FirstName = "John", LastName = "Doe", IsActive = true }
        };

        var participant1Round2 = new RoundParticipant
        {
            Id = 2,
            RoundId = 2,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 6.0,  // Handicap improved to 6.0 (9-hole = 3.0)
            CourseHandicap = 3,   // 9-hole course handicap
            TotalGrossStrokes = 45,
            TotalNetStrokes = 42, // 45 - 3 strokes = 42
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 1, FirstName = "John", LastName = "Doe", IsActive = true }
        };

        // Player 2: Single round, handicap 6, scored net 38
        var participant2Round1 = new RoundParticipant
        {
            Id = 3,
            RoundId = 1,
            PlayerId = 2,
            FlightId = 1,
            HandicapIndex = 12.0,
            CourseHandicap = 6,
            TotalGrossStrokes = 44,
            TotalNetStrokes = 38, // 44 - 6 strokes = 38
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 2, FirstName = "Jane", LastName = "Smith", IsActive = true }
        };

        _roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round> { round1, round2 });

        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default))
            .ReturnsAsync(new List<RoundParticipant> { participant1Round1, participant2Round1 });

        _roundRepo.Setup(r => r.GetParticipantsAsync(2, default))
            .ReturnsAsync(new List<RoundParticipant> { participant1Round2 });

        _roundRepo.Setup(r => r.GetHoleScoresAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new List<HoleScore>());

        var result = await _handler.Handle(new GetLeagueLeaderboardsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var leaderboards = result.Value!;

        // Player 2 should be first with avg net 38.0 (lower is better)
        // Player 1 should be second with avg net 41.0
        leaderboards.LowNet.Should().HaveCount(2);
        
        leaderboards.LowNet[0].PlayerId.Should().Be(2); // Jane - better avg net
        leaderboards.LowNet[0].AverageNetScore.Should().Be(38.0);
        leaderboards.LowNet[0].RoundsPlayed.Should().Be(1);

        leaderboards.LowNet[1].PlayerId.Should().Be(1); // John
        leaderboards.LowNet[1].AverageNetScore.Should().Be(41.0); // (40 + 42) / 2
        leaderboards.LowNet[1].RoundsPlayed.Should().Be(2);
    }

    [Fact]
    public async Task Handle_HalfFiltered_Par3SkinsCarryoverSpansHalfBoundaryWithinSeason()
    {
        // H1 round: a par-3 tie that's never resolved within H1 (skin is "lost" if
        // treated as half-scoped, but should carry into H2 since it's the same season).
        var h1Round = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 6, 15),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 9,
        };

        // H2 round: same par-3 hole, one clear winner this time — should be worth
        // 2 (1 + the carryover from the unresolved H1 tie), not 1.
        var h2Round = new Round
        {
            Id = 2,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 7, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 2,
            WeekNumber = 1,
        };

        var h1Alice = new RoundParticipant
        {
            Id = 1, RoundId = 1, PlayerId = 1, FlightId = 1,
            TotalGrossStrokes = 40, TotalNetStrokes = 36,
            HoleScores = new List<HoleScore> { new() { HoleNumber = 3, Par = 3, GrossStrokes = 4, NetStrokes = 3 } },
            Player = new Player { Id = 1, FirstName = "Alice", LastName = "A", IsActive = true },
        };
        var h1Bob = new RoundParticipant
        {
            Id = 2, RoundId = 1, PlayerId = 2, FlightId = 1,
            TotalGrossStrokes = 40, TotalNetStrokes = 36,
            HoleScores = new List<HoleScore> { new() { HoleNumber = 3, Par = 3, GrossStrokes = 4, NetStrokes = 3 } },
            Player = new Player { Id = 2, FirstName = "Bob", LastName = "B", IsActive = true },
        };

        var h2Alice = new RoundParticipant
        {
            Id = 3, RoundId = 2, PlayerId = 1, FlightId = 1,
            TotalGrossStrokes = 38, TotalNetStrokes = 34,
            HoleScores = new List<HoleScore> { new() { HoleNumber = 3, Par = 3, GrossStrokes = 2, NetStrokes = 1 } },
            Player = new Player { Id = 1, FirstName = "Alice", LastName = "A", IsActive = true },
        };
        var h2Bob = new RoundParticipant
        {
            Id = 4, RoundId = 2, PlayerId = 2, FlightId = 1,
            TotalGrossStrokes = 42, TotalNetStrokes = 38,
            HoleScores = new List<HoleScore> { new() { HoleNumber = 3, Par = 3, GrossStrokes = 4, NetStrokes = 3 } },
            Player = new Player { Id = 2, FirstName = "Bob", LastName = "B", IsActive = true },
        };

        _roundRepo.Setup(r => r.GetByHalfAsync(2, default))
            .ReturnsAsync(new List<Round> { h2Round });
        _roundRepo.Setup(r => r.GetBySeasonAsync(1, default))
            .ReturnsAsync(new List<Round> { h1Round, h2Round });

        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default))
            .ReturnsAsync(new List<RoundParticipant> { h1Alice, h1Bob });
        _roundRepo.Setup(r => r.GetParticipantsAsync(2, default))
            .ReturnsAsync(new List<RoundParticipant> { h2Alice, h2Bob });

        _roundRepo.Setup(r => r.GetHoleScoresAsync(1, default)).ReturnsAsync(h1Alice.HoleScores.ToList());
        _roundRepo.Setup(r => r.GetHoleScoresAsync(2, default)).ReturnsAsync(h1Bob.HoleScores.ToList());
        _roundRepo.Setup(r => r.GetHoleScoresAsync(3, default)).ReturnsAsync(h2Alice.HoleScores.ToList());
        _roundRepo.Setup(r => r.GetHoleScoresAsync(4, default)).ReturnsAsync(h2Bob.HoleScores.ToList());

        var result = await _handler.Handle(new GetLeagueLeaderboardsQuery(HalfId: 2), default);

        result.IsSuccess.Should().BeTrue();
        var leaderboards = result.Value!;

        // Alice wins the H2 par-3 skin, worth 2 (1 + the H1 carryover), not 1.
        leaderboards.Par3Skins.Should().ContainSingle(p => p.PlayerId == 1);
        var alice = leaderboards.Par3Skins.Single(p => p.PlayerId == 1);
        alice.TotalSkinsWon.Should().Be(1);
        alice.TotalSkinValue.Should().Be(2, "the unresolved H1 tie should carry into H2 within the same season");

        // H1 stats must not leak into the H2-filtered leaderboard.
        leaderboards.LowNet.Should().OnlyContain(e => e.RoundsPlayed == 1);
    }

    [Fact]
    public async Task Handle_SkippedWeekAndWithdrawnPlayers_AreExcludedFromAverage()
    {
        var round1 = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1
        };

        // Player skipped week - should NOT count toward rounds played
        var skippedParticipant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 10.0,
            CourseHandicap = 5,
            TotalGrossStrokes = null, // No scores entered
            TotalNetStrokes = null,
            IsWithdrawn = false,
            SkippedWeek = true, // SKIPPED
            Player = new Player { Id = 1, FirstName = "John", LastName = "Doe", IsActive = true }
        };

        // Player withdrew - should NOT count toward rounds played
        var withdrawnParticipant = new RoundParticipant
        {
            Id = 2,
            RoundId = 1,
            PlayerId = 2,
            FlightId = 1,
            HandicapIndex = 12.0,
            CourseHandicap = 6,
            TotalGrossStrokes = 50,
            TotalNetStrokes = 44,
            IsWithdrawn = true, // WITHDRAWN
            SkippedWeek = false,
            Player = new Player { Id = 2, FirstName = "Jane", LastName = "Smith", IsActive = true }
        };

        // Normal player with scores
        var normalParticipant = new RoundParticipant
        {
            Id = 3,
            RoundId = 1,
            PlayerId = 3,
            FlightId = 1,
            HandicapIndex = 8.0,
            CourseHandicap = 4,
            TotalGrossStrokes = 42,
            TotalNetStrokes = 38, // 42 - 4 = 38
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 3, FirstName = "Bob", LastName = "Wilson", IsActive = true }
        };

        _roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round> { round1 });

        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default))
            .ReturnsAsync(new List<RoundParticipant> { skippedParticipant, withdrawnParticipant, normalParticipant });

        _roundRepo.Setup(r => r.GetHoleScoresAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new List<HoleScore>());

        var result = await _handler.Handle(new GetLeagueLeaderboardsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var leaderboards = result.Value!;

        // Only player 3 should be in the leaderboard
        leaderboards.LowNet.Should().HaveCount(1);
        leaderboards.LowNet[0].PlayerId.Should().Be(3);
        leaderboards.LowNet[0].AverageNetScore.Should().Be(38.0);
        leaderboards.LowNet[0].RoundsPlayed.Should().Be(1);
    }
}
