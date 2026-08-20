using FluentAssertions;
using GolfLeague.Application.Statistics.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;
using static GolfLeague.Domain.Services.StablefordScoringService;

namespace GolfLeague.Tests.Application;

public class GetLeagueLeaderboardsHandicapVerificationTests
{
    private readonly Mock<IRoundRepository> _roundRepo;
    private readonly Mock<IPlayerRepository> _playerRepo;
    private readonly Mock<IPlayerHalfSettingRepository> _halfSettingRepo;
    private readonly GetLeagueLeaderboardsQueryHandler _handler;

    public GetLeagueLeaderboardsHandicapVerificationTests()
    {
        _roundRepo = new Mock<IRoundRepository>();
        _playerRepo = new Mock<IPlayerRepository>();
        _halfSettingRepo = new Mock<IPlayerHalfSettingRepository>();
        _roundRepo.Setup(r => r.GetClosestToPinWinnersForRoundsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RoundClosestToPin>());
        _halfSettingRepo.Setup(s => s.GetForHalfAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlayerHalfSetting>());
        _handler = new GetLeagueLeaderboardsQueryHandler(_roundRepo.Object, _playerRepo.Object, _halfSettingRepo.Object);
    }

    [Fact]
    public async Task Handle_VerifyHandicapAtTimeOfRound_IsUsedForNetCalculation()
    {
        // This test verifies that when a player's handicap changes between rounds,
        // each round uses the handicap that was in effect AT THE TIME OF THAT ROUND
        
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

        var round3 = new Round
        {
            Id = 3,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 15),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 3
        };

        // Player starts with handicap index 18.0 (9-hole = 9.0)
        // After round 1, improves to 16.0 (9-hole = 8.0)
        // After round 2, improves to 14.0 (9-hole = 7.0)
        
        // Round 1: CourseHandicap = 9 (from 18.0 index)
        // Player shoots 50 gross, gets 9 strokes, net = 41
        var participantRound1 = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 18.0,  // 18-hole index AT TIME OF ROUND 1
            CourseHandicap = 9,     // 9-hole course handicap (9 = 18/2)
            TotalGrossStrokes = 50,
            TotalNetStrokes = 41,    // 50 - 9 strokes = 41
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 1, FirstName = "John", LastName = "Doe", IsActive = true }
        };

        // Round 2: CourseHandicap = 8 (from 16.0 index)
        // Player shoots 48 gross, gets 8 strokes, net = 40
        var participantRound2 = new RoundParticipant
        {
            Id = 2,
            RoundId = 2,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 16.0,   // 18-hole index AT TIME OF ROUND 2
            CourseHandicap = 8,     // 9-hole course handicap (8 = 16/2)
            TotalGrossStrokes = 48,
            TotalNetStrokes = 40,    // 48 - 8 strokes = 40
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 1, FirstName = "John", LastName = "Doe", IsActive = true }
        };

        // Round 3: CourseHandicap = 7 (from 14.0 index)
        // Player shoots 46 gross, gets 7 strokes, net = 39
        var participantRound3 = new RoundParticipant
        {
            Id = 3,
            RoundId = 3,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 14.0,   // 18-hole index AT TIME OF ROUND 3
            CourseHandicap = 7,     // 9-hole course handicap (7 = 14/2)
            TotalGrossStrokes = 46,
            TotalNetStrokes = 39,    // 46 - 7 strokes = 39
            IsWithdrawn = false,
            SkippedWeek = false,
            Player = new Player { Id = 1, FirstName = "John", LastName = "Doe", IsActive = true }
        };

        _roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round> { round1, round2, round3 });

        _roundRepo.Setup(r => r.GetParticipantsForRoundsAsync(It.IsAny<IEnumerable<int>>(), default))
            .ReturnsAsync(new List<RoundParticipant> { participantRound1, participantRound2, participantRound3 });

        var result = await _handler.Handle(new GetLeagueLeaderboardsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var leaderboards = result.Value!;

        // The average net should be (41 + 40 + 39) / 3 = 40.0
        // Each round used its own handicap at the time:
        // - Round 1: handicap 9, net 41
        // - Round 2: handicap 8, net 40
        // - Round 3: handicap 7, net 39
        leaderboards.LowNet.Should().HaveCount(1);
        leaderboards.LowNet[0].PlayerId.Should().Be(1);
        leaderboards.LowNet[0].AverageNetScore.Should().Be(40.0);
        leaderboards.LowNet[0].RoundsPlayed.Should().Be(3);
    }

    [Theory]
    [InlineData(18.0, 113, 72.0, 72, 9)]   // 18-hole index, slope 113 = 9-hole course handicap of 9
    [InlineData(16.0, 113, 72.0, 72, 8)]   // 16-hole index, slope 113 = 9-hole course handicap of 8
    [InlineData(14.0, 113, 72.0, 72, 7)]   // 14-hole index, slope 113 = 9-hole course handicap of 7
    [InlineData(10.0, 130, 72.0, 72, 6)]   // 10-hole index, slope 130 = full CH 11.5 -> round to 12 -> 9-hole = 6
    public void CourseHandicap_NineHoleCalculation(double handicapIndex, int slopeRating, double courseRating, int par, int expectedNineHoleCourseHandicap)
    {
        // Verify that the CourseHandicap function correctly calculates 9-hole handicaps
        var result = CourseHandicap(handicapIndex, slopeRating, courseRating, par, RoundType.NineHole);
        result.Should().Be(expectedNineHoleCourseHandicap);
    }
}
