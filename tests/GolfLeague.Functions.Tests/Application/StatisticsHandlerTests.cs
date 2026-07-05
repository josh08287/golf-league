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
