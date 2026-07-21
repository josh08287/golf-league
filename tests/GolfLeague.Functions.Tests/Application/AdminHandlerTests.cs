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
    private static GetAuditLogQueryHandler BuildHandler(
        Mock<IAuditRepository> repo,
        Mock<IAppUserRepository>? appUserRepo = null,
        Mock<IPlayerRepository>? playerRepo = null,
        Mock<IRoundRepository>? roundRepo = null,
        Mock<IFlightRepository>? flightRepo = null,
        Mock<ICourseRepository>? courseRepo = null,
        Mock<ISeasonRepository>? seasonRepo = null,
        Mock<IInviteRepository>? inviteRepo = null,
        Mock<ITeeTimeRepository>? teeTimeRepo = null)
    {
        // Only apply an empty-result default when the caller didn't supply
        // their own mock — otherwise this would clobber the caller's Setup
        // (Moq: the last-registered matching setup wins).
        if (appUserRepo is null)
        {
            appUserRepo = new Mock<IAppUserRepository>();
            appUserRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
                .ReturnsAsync(new List<AppUser>());
        }
        if (playerRepo is null)
        {
            playerRepo = new Mock<IPlayerRepository>();
            playerRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Player>());
        }
        if (roundRepo is null)
        {
            roundRepo = new Mock<IRoundRepository>();
            roundRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Round>());
        }
        if (flightRepo is null)
        {
            flightRepo = new Mock<IFlightRepository>();
            flightRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Flight>());
        }
        if (courseRepo is null)
        {
            courseRepo = new Mock<ICourseRepository>();
            courseRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Course>());
        }
        if (seasonRepo is null)
        {
            seasonRepo = new Mock<ISeasonRepository>();
            seasonRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Season>());
        }
        if (inviteRepo is null)
        {
            inviteRepo = new Mock<IInviteRepository>();
            inviteRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<PlayerInvite>());
        }
        if (teeTimeRepo is null)
        {
            teeTimeRepo = new Mock<ITeeTimeRepository>();
        }

        return new GetAuditLogQueryHandler(
            repo.Object, appUserRepo.Object, playerRepo.Object, roundRepo.Object, flightRepo.Object, courseRepo.Object,
            seasonRepo.Object, inviteRepo.Object, teeTimeRepo.Object);
    }

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

        var handler = BuildHandler(repo);
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
        // "admin" doesn't parse as a Guid, so the user falls back to "Unknown user";
        // the entity has no matching Player row, so it falls back to "Player #42".
        entry.Entity.Should().Be("Player #42");
        entry.User.Should().Be("Unknown user");
        entry.Timestamp.Should().Contain("2026-01-15");
        entry.Details.Should().Be("{}");
    }

    [Fact]
    public async Task Handle_ResolvesUserAndEntityDisplayNames()
    {
        var appUserId = Guid.NewGuid();
        var items = new List<AuditLog>
        {
            new()
            {
                Id = 1,
                Action = "TeeTimeSelected",
                EntityType = "Round",
                EntityId = "5",
                UserId = appUserId.ToString(),
                Timestamp = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            }
        };

        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(1, int.MaxValue, default)).ReturnsAsync((items, 1));

        var appUserRepo = new Mock<IAppUserRepository>();
        appUserRepo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(new List<AppUser> { new() { Id = appUserId, Email = "jane@example.com" } });

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Player> { new() { Id = 9, FirstName = "Jane", LastName = "Doe", AppUserId = appUserId } });

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetAllAsync(default))
            .ReturnsAsync(new List<Round> { new() { Id = 5, WeekNumber = 3, RoundDate = new DateOnly(2026, 1, 12) } });

        var handler = BuildHandler(repo, appUserRepo, playerRepo, roundRepo);
        var result = await handler.Handle(new GetAuditLogQuery(1, 25), default);

        var entry = result.Value!.Items[0];
        entry.User.Should().Be("Jane Doe");
        entry.Entity.Should().Be("Week 3 — Jan 12, 2026");
    }

    [Fact]
    public async Task Handle_WithNoItems_ReturnsEmptyList()
    {
        var repo = new Mock<IAuditRepository>();
        repo.Setup(r => r.GetPagedAsync(1, int.MaxValue, default)).ReturnsAsync((new List<AuditLog>(), 0));

        var handler = BuildHandler(repo);
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

        var handler = BuildHandler(repo);
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
    public async Task Handle_WhenPlayerHandicapChangedSinceRound_RefreshesStaleSnapshotAndCourseHandicap()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.Finalized,
            RoundDate = new DateOnly(2026, 3, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1,
            NineHoleSide = NineHoleSide.Front
        };

        var course = new Course { Id = 1, Name = "Test Course", SlopeRating = 123, CourseRating = 70.1 };

        var courseHoles = Enumerable.Range(1, 18)
            .Select(i => new CourseHole { Id = i, CourseId = 1, HoleNumber = i, Par = 4, StrokeIndex = i })
            .ToList();

        // Snapshot taken when the round was created: player's 18-hole index was 18.0 at the time.
        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 18.0,
            CourseHandicap = 10,
            IsWithdrawn = false,
            SkippedWeek = false,
            HoleScores = new List<HoleScore>()
        };

        _roundRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Round> { round });
        _courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        _courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(courseHoles);
        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default)).ReturnsAsync(new List<RoundParticipant> { participant });

        // Player's handicap has since improved to 8.8 (18-hole), effective before the round date.
        _handicapRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Handicap>
        {
            new() { Id = 1, PlayerId = 1, HandicapIndex = 8.8, EffectiveDate = new DateOnly(2026, 2, 15) },
            new() { Id = 2, PlayerId = 1, HandicapIndex = 18.0, EffectiveDate = new DateOnly(2026, 1, 1) },
        });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        participant.HandicapIndex.Should().Be(8.8);
        // Round(8.8 * 123/113 + (70.1 - 72)) = Round(7.685) = 8; halved for 9-hole -> 4
        participant.CourseHandicap.Should().Be(4);
        _roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Once);
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
    public async Task Handle_WhenRoundIsScheduledAndPlayerHandicapChanged_RefreshesCourseHandicapBeforeFinalization()
    {
        var round = new Round
        {
            Id = 1,
            Status = RoundStatus.Scheduled,
            RoundDate = new DateOnly(2026, 3, 1),
            CourseId = 1,
            SeasonId = 1,
            HalfId = 1,
            WeekNumber = 1,
            NineHoleSide = NineHoleSide.Front
        };

        var course = new Course { Id = 1, Name = "Test Course", SlopeRating = 123, CourseRating = 70.1 };
        var courseHoles = Enumerable.Range(1, 18)
            .Select(i => new CourseHole { Id = i, CourseId = 1, HoleNumber = i, Par = 4, StrokeIndex = i })
            .ToList();

        // Snapshot taken when the round was scheduled: player's 18-hole index was 18.0 at the time.
        var participant = new RoundParticipant
        {
            Id = 1,
            RoundId = 1,
            PlayerId = 1,
            FlightId = 1,
            HandicapIndex = 18.0,
            CourseHandicap = 10,
            IsWithdrawn = false,
            SkippedWeek = false,
            HoleScores = new List<HoleScore>()
        };

        _roundRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Round> { round });
        _courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        _courseRepo.Setup(r => r.GetHolesAsync(1, default)).ReturnsAsync(courseHoles);
        _roundRepo.Setup(r => r.GetParticipantsAsync(1, default)).ReturnsAsync(new List<RoundParticipant> { participant });

        // Player's handicap has since improved to 8.8 (18-hole), effective before the round date.
        _handicapRepo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(new List<Handicap>
        {
            new() { Id = 1, PlayerId = 1, HandicapIndex = 8.8, EffectiveDate = new DateOnly(2026, 2, 15) },
            new() { Id = 2, PlayerId = 1, HandicapIndex = 18.0, EffectiveDate = new DateOnly(2026, 1, 1) },
        });

        var result = await _handler.Handle(new RecalculateAllRoundsCommand("admin"), default);

        result.IsSuccess.Should().BeTrue();
        // Scheduled rounds are never score-recalculated (no hole scores exist yet).
        result.Value!.RoundsProcessed.Should().Be(0);
        participant.HandicapIndex.Should().Be(8.8);
        // Round(8.8 * 123/113 + (70.1 - 72)) = Round(7.685) = 8; halved for 9-hole -> 4
        participant.CourseHandicap.Should().Be(4);
        _roundRepo.Verify(r => r.UpdateParticipantAsync(participant, default), Times.Once);
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
