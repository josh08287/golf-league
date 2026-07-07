using FluentAssertions;
using GolfLeague.Application.Admin;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class GetAuditLogQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMappedPagedResult()
    {
        var items = new List<AuditLog>
        {
            new()
            {
                Id = 1,
                Action = "Create",
                EntityType = "Player",
                EntityId = "42",
                UserId = "admin",
                Timestamp = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
                AfterJson = "{}"
            }
        };

        var repo = new Mock<IAuditRepository>();
        // Handler now pulls everything (sort happens in-memory) and pages
        // after sorting, so it always asks the repo for page 1, MaxValue.
        repo.Setup(r => r.GetPagedAsync(1, int.MaxValue, default)).ReturnsAsync((items, 1));

        var handler = new GetAuditLogQueryHandler(repo.Object);
        var result = await handler.Handle(new GetAuditLogQuery(1, 25), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(1);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(25);

        var entry = result.Value.Items[0];
        entry.Id.Should().Be(1);
        entry.Action.Should().Be("Create");
        entry.EntityType.Should().Be("Player");
        entry.EntityId.Should().Be("42");
        entry.UserId.Should().Be("admin");
        entry.Timestamp.Should().Contain("2026-01-15");
        entry.Details.Should().Be("{}");
    }

    [Fact]
    public async Task Handle_WithNoItems_ReturnsEmptyList()
    {
        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(1, int.MaxValue, default)).ReturnsAsync((new List<AuditLog>(), 0));

        var handler = new GetAuditLogQueryHandler(repo.Object);
        var result = await handler.Handle(new GetAuditLogQuery(1, 25), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MapsTimestampToIso8601()
    {
        var timestamp = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var items = new List<AuditLog>
        {
            new() { Id = 1, Action = "Update", EntityType = "Round", EntityId = "1",
                    UserId = "admin", Timestamp = timestamp, AfterJson = null }
        };

        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(1, int.MaxValue, default)).ReturnsAsync((items, 1));

        var handler = new GetAuditLogQueryHandler(repo.Object);
        var result = await handler.Handle(new GetAuditLogQuery(2, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        // Page 2 of a single-item set is empty after in-memory paging.
        result.Value.Items.Should().BeEmpty();
    }
}

public class RecalculateAllRoundsCommandHandlerTests
{
    private readonly Mock<IRoundRepository> _roundRepo;
    private readonly Mock<ICourseRepository> _courseRepo;
    private readonly Mock<IHandicapRepository> _handicapRepo;
    private readonly Mock<IFlightRepository> _flightRepo;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<RecalculateAllRoundsCommandHandler>> _logger;
    private readonly RecalculateAllRoundsCommandHandler _handler;

    public RecalculateAllRoundsCommandHandlerTests()
    {
        _roundRepo = new Mock<IRoundRepository>();
        _courseRepo = new Mock<ICourseRepository>();
        _handicapRepo = new Mock<IHandicapRepository>();
        _flightRepo = new Mock<IFlightRepository>();
        _flightRepo.Setup(r => r.GetMembershipsByHalfAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new List<FlightMembership>());
        _logger = new Mock<Microsoft.Extensions.Logging.ILogger<RecalculateAllRoundsCommandHandler>>();
        _handler = new RecalculateAllRoundsCommandHandler(
            _roundRepo.Object,
            _courseRepo.Object,
            _handicapRepo.Object,
            _flightRepo.Object,
            _logger.Object);
    }

    [Fact]
    public async Task Handle_WithNoFinalizedRounds_ReturnsZeroCounts()
    {
        _roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round>());

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoundsProcessed.Should().Be(0);
        result.Value.ParticipantsProcessed.Should().Be(0);
        result.Value.HoleScoresUpdated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithFinalizedRound_RecalculatesParticipantScores()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1,
            NineHoleSide = NineHoleSide.Front
        };

        var course = new Course
        {
            Id = 1,
            Name = "Test Course",
            SlopeRating = 113,
            CourseRating = 72.0
        };

        var courseHoles = Enumerable.Range(1, 9).Select(i => new CourseHole
        {
            Id = i,
            CourseId = 1,
            HoleNumber = i,
            Par = i <= 2 ? 3 : (i <= 5 ? 4 : (i == 6 ? 5 : 4)),
            StrokeIndex = i
        }).ToList();

        var holeScores = courseHoles.Select(h => new HoleScore
        {
            Id = h.HoleNumber,
            ParticipantId = 1,
            HoleNumber = h.HoleNumber,
            Par = h.Par,
            StrokeIndex = h.StrokeIndex,
            GrossStrokes = 5,
            HandicapStrokes = 0,
            NetStrokes = 5,
            GrossStablefordPoints = 1,
            NetStablefordPoints = 2
        }).ToList();

        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 18.0,
            CourseHandicap = 9,
            TotalGrossStrokes = 45,
            TotalNetStrokes = 36,
            IsWithdrawn = false,
            SkippedWeek = false,
            HoleScores = holeScores  // Set HoleScores directly on participant
        };

        _roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round> { round });

        _courseRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(course);

        _courseRepo.Setup(r => r.GetHolesAsync(1, default))
            .ReturnsAsync(courseHoles);

        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default))
            .ReturnsAsync(new List<RoundParticipant> { participant });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoundsProcessed.Should().Be(1);
        result.Value.ParticipantsProcessed.Should().Be(1);
        result.Value.HoleScoresUpdated.Should().Be(9); // 9 holes recalculated

        // Verify participant was updated
        _roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Once);
    }

    [Fact]
    public async Task Handle_SkippedWeekAndWithdrawnParticipants_AreExcluded()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1,
            NineHoleSide = NineHoleSide.Front
        };

        var course = new Course
        {
            Id = 1,
            Name = "Test Course",
            SlopeRating = 113,
            CourseRating = 72.0
        };

        var skippedParticipant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 18.0,
            CourseHandicap = 9,
            IsWithdrawn = false,
            SkippedWeek = true // SKIPPED
        };

        var withdrawnParticipant = new RoundParticipant
        {
            Id = 2,
            RoundId = 1,
            PlayerId = 2,
            FlightId = 1,
            HandicapIndex = 18.0,
            CourseHandicap = 9,
            IsWithdrawn = true, // WITHDRAWN
            SkippedWeek = false
        };

        _roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round> { round });

        _courseRepo.Setup(r => r.GetByIdAsync(1, default))
            .ReturnsAsync(course);

        _courseRepo.Setup(r => r.GetHolesAsync(1, default))
            .ReturnsAsync(new List<CourseHole>());

        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default))
            .ReturnsAsync(new List<RoundParticipant> { skippedParticipant, withdrawnParticipant });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoundsProcessed.Should().Be(1);
        result.Value.ParticipantsProcessed.Should().Be(0); // None processed
        result.Value.HoleScoresUpdated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPlayerFlightMembershipChanged_ResyncsParticipantFlightId()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 1, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1,
            NineHoleSide = NineHoleSide.Front
        };

        var course = new Course { Id = 1, Name = "Test Course", SlopeRating = 113, CourseRating = 72.0 };

        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1, // Stale: player was in flight 1 when the round was played
            HandicapIndex = 18.0,
            CourseHandicap = 9,
            IsWithdrawn = false,
            SkippedWeek = false,
            HoleScores = new List<HoleScore>()
        };

        _roundRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Round> { round });
        _courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        _courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(new List<CourseHole>());
        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default)).ReturnsAsync(new List<RoundParticipant> { participant });

        // Admin has since moved the player to flight 2 for this half.
        _flightRepo.Setup(r => r.GetMembershipsByHalfAsync(1, default))
            .ReturnsAsync(new List<FlightMembership> { new() { PlayerId = 1, FlightId = 2, HalfId = 1 } });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlightAssignmentsUpdated.Should().Be(1);
        participant.FlightId.Should().Be(2);
        // Updated twice: once by the flight resync pass, once by score recalculation.
        _roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenRoundIsInProgress_StillResyncsFlightIdSoLiveLeaderboardIsAccurate()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.InProgress,
            RoundDate = new DateOnly(2026, 1, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1,
            NineHoleSide = NineHoleSide.Front
        };

        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1, // Stale: player was in flight 1 when the round started
            HandicapIndex = 18.0,
            CourseHandicap = 9,
            IsWithdrawn = false,
            SkippedWeek = false,
            HoleScores = new List<HoleScore>()
        };

        _roundRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Round> { round });
        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default)).ReturnsAsync(new List<RoundParticipant> { participant });

        // Admin has since moved the player to flight 2 for this half, while the round is still live.
        _flightRepo.Setup(r => r.GetMembershipsByHalfAsync(1, default))
            .ReturnsAsync(new List<FlightMembership> { new() { PlayerId = 1, FlightId = 2, HalfId = 1 } });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlightAssignmentsUpdated.Should().Be(1);
        participant.FlightId.Should().Be(2);
        // In-progress rounds are never score-recalculated, only flight-resynced.
        result.Value.RoundsProcessed.Should().Be(0);
        _roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRoundIsScheduled_StillResyncsFlightIdSoRoundsPageIsAccurate()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.Scheduled,
            RoundDate = new DateOnly(2026, 2, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 2,
            NineHoleSide = NineHoleSide.Front
        };

        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1, // Stale: player was in flight 1 when the schedule was generated
            HandicapIndex = 18.0,
            CourseHandicap = 9,
            IsWithdrawn = false,
            SkippedWeek = false,
            HoleScores = new List<HoleScore>()
        };

        _roundRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Round> { round });
        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default)).ReturnsAsync(new List<RoundParticipant> { participant });

        // Admin has since moved the player to flight 2 for this half, before the round has started.
        _flightRepo.Setup(r => r.GetMembershipsByHalfAsync(1, default))
            .ReturnsAsync(new List<FlightMembership> { new() { PlayerId = 1, FlightId = 2, HalfId = 1 } });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlightAssignmentsUpdated.Should().Be(1);
        participant.FlightId.Should().Be(2);
        // Scheduled rounds are never score-recalculated, only flight-resynced.
        result.Value.RoundsProcessed.Should().Be(0);
        _roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Once);
    }
}
