using FluentAssertions;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CreateRoundCommandHandlerTests
{
    private static Course MakeCourse() => new() { Id = 1, Name = "Course", CourseRating = 72.0, SlopeRating = 113 };
    private static Flight MakeFlight() => new() { Id = 1, Name = "A Flight" };

    [Fact]
    public async Task Handle_WhenCourseNotFound_ReturnsFail()
    {
        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Course?)null);
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var flightRepo = new Mock<IFlightRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 99, DateOnly.FromDateTime(DateTime.UtcNow), null, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Course");
    }

    [Fact]
    public async Task Handle_WhenFlightNotFound_ReturnsFail()
    {
        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(MakeCourse());
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Flight?)null);
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 99, new List<int> { 99 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Flight");
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesRoundAndParticipants()
    {
        var course = MakeCourse();
        var flight = MakeFlight();
        var player = new Player { Id = 1, FirstName = "J", LastName = "D" };

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership> { 
            new FlightMembership { FlightId = 1, PlayerId = 1, Player = player } 
        });
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync(new Handicap { HandicapIndex = 10.0 });

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), "test", "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlightName.Should().Be("A Flight");
        result.Value.CourseName.Should().Be("Course");
        roundRepo.Verify(r => r.AddAsync(It.IsAny<Round>(), default), Times.Once);
        roundRepo.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenParticipantPlayerNotFound_SkipsParticipant()
    {
        var course = MakeCourse();
        var flight = MakeFlight();

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership> { 
            new FlightMembership { FlightId = 1, PlayerId = 99 } 
        });
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        roundRepo.Verify(r => r.AddParticipantAsync(It.IsAny<RoundParticipant>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_CreatesNineHoleFrontRound_WithCorrectDefaults()
    {
        var course = MakeCourse();
        var flight = MakeFlight();

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership>());
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, "admin",
            RoundType.NineHole, NineHoleSide.Front);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoundType.Should().Be(RoundType.NineHole);
        result.Value.NineHoleSide.Should().Be(NineHoleSide.Front);
        roundRepo.Verify(r => r.AddAsync(It.Is<Round>(round =>
            round.RoundType == RoundType.NineHole &&
            round.NineHoleSide == NineHoleSide.Front), default), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesNineHoleBackRound_WithCorrectProperties()
    {
        var course = MakeCourse();
        var flight = MakeFlight();

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership>());
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, "admin",
            RoundType.NineHole, NineHoleSide.Back);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoundType.Should().Be(RoundType.NineHole);
        result.Value.NineHoleSide.Should().Be(NineHoleSide.Back);
    }

    [Fact]
    public async Task Handle_CreatesEighteenHoleRound_WithCorrectProperties()
    {
        var course = MakeCourse();
        var flight = MakeFlight();

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership>());
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), null, "admin",
            RoundType.EighteenHole, NineHoleSide.NotApplicable);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RoundType.Should().Be(RoundType.EighteenHole);
    }

    [Fact]
    public async Task Handle_NineHoleRound_CalculatesHalfCourseHandicap()
    {
        var course = MakeCourse();
        var flight = MakeFlight();
        var player = new Player { Id = 1, FirstName = "J", LastName = "D" };

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership> { 
            new FlightMembership { FlightId = 1, PlayerId = 1, Player = player } 
        });
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        // Handicap 18 on slope 113 = 18 course handicap for 18 holes
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync(new Handicap { HandicapIndex = 18.0 });

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);

        // 9-hole round should have ~9 course handicap (half of 18)
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), "test", "admin",
            RoundType.NineHole, NineHoleSide.Front);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        // 18 * 113 / 113 = 18, rounded to 18 for 18 holes, half for 9 holes = 9
        roundRepo.Verify(r => r.AddParticipantAsync(It.Is<RoundParticipant>(p =>
            p.CourseHandicap == 9), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleFlights_CreatesRoundWithAllParticipants()
    {
        var course = MakeCourse();
        var flight1 = MakeFlight();
        var flight2 = new Flight { Id = 2, Name = "B Flight", SeasonId = 1 };
        var player1 = new Player { Id = 1, FirstName = "A", LastName = "One", IsActive = true };
        var player2 = new Player { Id = 2, FirstName = "B", LastName = "Two", IsActive = true };

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight1);
        flightRepo.Setup(r => r.GetByIdAsync(2, default)).ReturnsAsync(flight2);
        flightRepo.Setup(r => r.GetMembershipsAsync(1, default)).ReturnsAsync(new List<FlightMembership> { 
            new FlightMembership { FlightId = 1, PlayerId = 1, Player = player1 } 
        });
        flightRepo.Setup(r => r.GetMembershipsAsync(2, default)).ReturnsAsync(new List<FlightMembership> { 
            new FlightMembership { FlightId = 2, PlayerId = 2, Player = player2 } 
        });
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player1);
        playerRepo.Setup(r => r.GetByIdAsync(2, default)).ReturnsAsync(player2);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync(new Handicap { HandicapIndex = 10.0 });
        handicapRepo.Setup(r => r.GetCurrentAsync(2, default)).ReturnsAsync(new Handicap { HandicapIndex = 15.0 });

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);

        // Create round with 2 flights
        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1, 2 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), "test", "admin",
            RoundType.NineHole, NineHoleSide.Front);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ParticipantCount.Should().Be(2);
        result.Value.FlightName.Should().Be("2 Flights");
        
        // Verify round was created with first flight for backwards compatibility
        roundRepo.Verify(r => r.AddAsync(It.Is<Round>(round => round.FlightId == 1), default), Times.Once);
        
        // Verify participants from both flights were added with correct FlightIds
        roundRepo.Verify(r => r.AddParticipantAsync(It.Is<RoundParticipant>(p =>
            p.PlayerId == 1 && p.FlightId == 1), default), Times.Once);
        roundRepo.Verify(r => r.AddParticipantAsync(It.Is<RoundParticipant>(p =>
            p.PlayerId == 2 && p.FlightId == 2), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleFlights_WhenOneFlightNotFound_ReturnsFail()
    {
        var course = MakeCourse();
        var flight1 = MakeFlight();

        var roundRepo = new Mock<IRoundRepository>();
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight1);
        flightRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Flight?)null);
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreateRoundCommandHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object, handicapRepo.Object, flightRepo.Object);

        var cmd = new CreateRoundCommand(1, 1, new List<int> { 1, 99 }, 1, DateOnly.FromDateTime(DateTime.UtcNow), "test", "admin",
            RoundType.NineHole, NineHoleSide.Front);

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Flight with ID 99 not found");
    }
}

public class FinalizeRoundCommandHandlerTests
{
    private static Round MakeInProgressRound() => new()
    {
        Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1,
        RoundDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Status = RoundStatus.InProgress,
        Participants = []
    };

    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Round?)null);
        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(),
            Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IFlightRepository>());

        var result = await handler.Handle(new FinalizeRoundCommand(99, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenAlreadyFinalized_ReturnsFail()
    {
        var round = MakeInProgressRound();
        round.Status = RoundStatus.Finalized;
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(),
            Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IFlightRepository>());

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already finalized");
    }

    [Fact]
    public async Task Handle_WhenCancelled_ReturnsFail()
    {
        var round = MakeInProgressRound();
        round.Status = RoundStatus.Cancelled;
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, Mock.Of<ICourseRepository>(),
            Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>(), Mock.Of<IFlightRepository>());

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Handle_WhenCourseNotFound_ReturnsFail()
    {
        var round = MakeInProgressRound();
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Course?)null);
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Flight { Id = 1, Name = "F" });

        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, courseRepo.Object,
            Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>(), flightRepo.Object);

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Course");
    }

    [Fact]
    public async Task Handle_WhenFlightNotFound_ReturnsFail()
    {
        var round = MakeInProgressRound();
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Course { Id = 1, Name = "C", CourseRating = 72.0, SlopeRating = 113 });
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Flight?)null);

        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, courseRepo.Object,
            Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>(), flightRepo.Object);

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Flight");
    }

    [Fact]
    public async Task Handle_WhenValid_FinalizesAndUpdatesHandicaps()
    {
        var participant = new RoundParticipant
        {
            Id = 1, PlayerId = 1, IsWithdrawn = false, TotalGrossStrokes = 85,
            HoleScores = []
        };
        var round = MakeInProgressRound();
        round.RoundType = RoundType.EighteenHole;
        round.NineHoleSide = NineHoleSide.NotApplicable;
        round.Participants = [participant];

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Course { Id = 1, Name = "C", CourseRating = 72.0, SlopeRating = 113 });
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Flight { Id = 1, Name = "F" });
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetLast20DifferentialsAsync(1, default)).ReturnsAsync(new List<double> { 10.0, 12.0 });
        var playerRepo = new Mock<IPlayerRepository>();

        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, courseRepo.Object, handicapRepo.Object, playerRepo.Object, flightRepo.Object);

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(RoundStatus.Finalized);
        roundRepo.Verify(r => r.UpdateAsync(It.Is<Round>(r => r.Status == RoundStatus.Finalized), default), Times.Once);
        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_SkipsWithdrawnParticipants()
    {
        var participant = new RoundParticipant { Id = 1, PlayerId = 1, IsWithdrawn = true, TotalGrossStrokes = 85 };
        var round = MakeInProgressRound();
        round.Participants = [participant];

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Course { Id = 1, Name = "C", CourseRating = 72.0, SlopeRating = 113 });
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Flight { Id = 1, Name = "F" });
        var handicapRepo = new Mock<IHandicapRepository>();
        var playerRepo = new Mock<IPlayerRepository>();

        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, courseRepo.Object, handicapRepo.Object, playerRepo.Object, flightRepo.Object);

        await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_NineHoleRound_StoresNineHoleDifferential_DoesNotUpdateHandicap()
    {
        var participant = new RoundParticipant
        {
            Id = 1, PlayerId = 1, IsWithdrawn = false, TotalGrossStrokes = 40,
            HoleScores = []
        };
        var round = MakeInProgressRound();
        round.RoundType = RoundType.NineHole;
        round.NineHoleSide = NineHoleSide.Front;
        round.Participants = [participant];

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Course { Id = 1, Name = "C", CourseRating = 72.0, SlopeRating = 113 });
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Flight { Id = 1, Name = "F" });
        var handicapRepo = new Mock<IHandicapRepository>();
        var playerRepo = new Mock<IPlayerRepository>();

        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, courseRepo.Object, handicapRepo.Object, playerRepo.Object, flightRepo.Object);

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeTrue();
        // For 9-hole rounds, we store the differential but don't update handicap
        handicapRepo.Verify(r => r.AddDifferentialAsync(1, It.IsAny<double>(), It.IsAny<DateOnly>(), default), Times.Once);
        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_EighteenHoleRound_StoresDifferential_AndUpdatesHandicap()
    {
        var participant = new RoundParticipant
        {
            Id = 1, PlayerId = 1, IsWithdrawn = false, TotalGrossStrokes = 85,
            HoleScores = []
        };
        var round = MakeInProgressRound();
        round.RoundType = RoundType.EighteenHole;
        round.NineHoleSide = NineHoleSide.NotApplicable;
        round.Participants = [participant];

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Course { Id = 1, Name = "C", CourseRating = 72.0, SlopeRating = 113 });
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(new Flight { Id = 1, Name = "F" });
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetLast20DifferentialsAsync(1, default)).ReturnsAsync(new List<double> { 10.0, 12.0 });
        var playerRepo = new Mock<IPlayerRepository>();

        var handler = new FinalizeRoundCommandHandler(roundRepo.Object, courseRepo.Object, handicapRepo.Object, playerRepo.Object, flightRepo.Object);

        var result = await handler.Handle(new FinalizeRoundCommand(1, "admin"), default);

        result.IsSuccess.Should().BeTrue();
        // For 18-hole rounds, we store the differential AND update handicap
        handicapRepo.Verify(r => r.AddDifferentialAsync(1, It.IsAny<double>(), It.IsAny<DateOnly>(), default), Times.Once);
        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Once);
    }
}

public class GetRoundQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenNotFound_ReturnsFail()
    {
        var repo = new Mock<IRoundRepository>();
        repo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Round?)null);
        var handler = new GetRoundQueryHandler(repo.Object);

        var result = await handler.Handle(new GetRoundQuery(99), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenFound_ReturnsDto()
    {
        var round = new Round
        {
            Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1,
            RoundDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = RoundStatus.Scheduled,
            Flight = new Flight { Name = "A Flight" },
            Course = new Course { Name = "Course A" },
            Participants = []
        };
        var repo = new Mock<IRoundRepository>();
        repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var handler = new GetRoundQueryHandler(repo.Object);

        var result = await handler.Handle(new GetRoundQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlightName.Should().Be("A Flight");
        result.Value.CourseName.Should().Be("Course A");
    }
}

public class GetRoundsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithoutSeasonId_GetsAllRounds()
    {
        var repo = new Mock<IRoundRepository>();
        var rounds = new List<Round>
        {
            new() { Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1,
                Flight = new Flight { Name = "F" }, Course = new Course { Name = "C" },
                Participants = [] }
        };
        repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(rounds);
        var handler = new GetRoundsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetRoundsQuery(null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        repo.Verify(r => r.GetAllAsync(default), Times.Once);
        repo.Verify(r => r.GetBySeasonAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WithSeasonId_GetsSeasonRounds()
    {
        var repo = new Mock<IRoundRepository>();
        repo.Setup(r => r.GetBySeasonAsync(5, default)).ReturnsAsync(new List<Round>());
        var handler = new GetRoundsQueryHandler(repo.Object);

        await handler.Handle(new GetRoundsQuery(5), default);

        repo.Verify(r => r.GetBySeasonAsync(5, default), Times.Once);
        repo.Verify(r => r.GetAllAsync(default), Times.Never);
    }
}

public class GetRoundParticipantsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var repo = new Mock<IRoundRepository>();
        repo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Round?)null);
        var handler = new GetRoundParticipantsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetRoundParticipantsQuery(99), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenFound_ReturnsSortedParticipants()
    {
        var player1 = new Player { Id = 1, FirstName = "Bob", LastName = "Anderson" };
        var player2 = new Player { Id = 2, FirstName = "Alice", LastName = "Anderson" };
        var round = new Round
        {
            Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1,
            RoundDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Participants = [
                new() { Id = 1, RoundId = 1, PlayerId = 1, Player = player1, HandicapIndex = 10, CourseHandicap = 10 },
                new() { Id = 2, RoundId = 1, PlayerId = 2, Player = player2, HandicapIndex = 12, CourseHandicap = 12 }
            ]
        };

        var repo = new Mock<IRoundRepository>();
        repo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        var handler = new GetRoundParticipantsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetRoundParticipantsQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        // Sorted by LastName then FirstName: Alice Anderson before Bob Anderson
        result.Value[0].PlayerName.Should().Be("Alice Anderson");
    }
}

public class GetRoundScorecardsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Round?)null);
        var courseRepo = new Mock<ICourseRepository>();
        var handler = new GetRoundScorecardsQueryHandler(roundRepo.Object, courseRepo.Object);

        var result = await handler.Handle(new GetRoundScorecardsQuery(99), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenFound_ReturnsScorecardsWithHoles()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe" };
        var round = new Round
        {
            Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1,
            RoundDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        var participants = new List<RoundParticipant>
        {
            new()
            {
                Id = 1, RoundId = 1, PlayerId = 1, Player = player,
                HandicapIndex = 10, CourseHandicap = 10,
                TotalGrossStrokes = 80, TotalNetStrokes = 70, TotalStablefordPoints = 25,
                HoleScores = [
                    new HoleScore { HoleNumber = 1, Par = 4, GrossStrokes = 5, NetStrokes = 4, StrokeIndex = 1 }
                ]
            }
        };
        var course = new Course { Id = 1, Name = "Golf Club" };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantsAsync(1, default)).ReturnsAsync(participants);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var handler = new GetRoundScorecardsQueryHandler(roundRepo.Object, courseRepo.Object);

        var result = await handler.Handle(new GetRoundScorecardsQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].PlayerName.Should().Be("John Doe");
        result.Value.Data[0].CourseName.Should().Be("Golf Club");
        result.Value.Data[0].Holes.Should().HaveCount(1);
    }
}

public class GetPlayerScorecardQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenRoundNotFound_ReturnsFail()
    {
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Round?)null);
        var handler = new GetPlayerScorecardQueryHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>());

        var result = await handler.Handle(new GetPlayerScorecardQuery(99, 1), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenParticipantNotFound_ReturnsFail()
    {
        var round = new Round { Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1, RoundDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 99, default)).ReturnsAsync((RoundParticipant?)null);

        var handler = new GetPlayerScorecardQueryHandler(roundRepo.Object, Mock.Of<ICourseRepository>(), Mock.Of<IPlayerRepository>());

        var result = await handler.Handle(new GetPlayerScorecardQuery(1, 99), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a participant");
    }

    [Fact]
    public async Task Handle_WhenCourseNotFound_ReturnsFail()
    {
        var round = new Round { Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1, RoundDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var participant = new RoundParticipant { Id = 1, PlayerId = 1 };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Course?)null);

        var handler = new GetPlayerScorecardQueryHandler(roundRepo.Object, courseRepo.Object, Mock.Of<IPlayerRepository>());

        var result = await handler.Handle(new GetPlayerScorecardQuery(1, 1), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Course");
    }

    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        var round = new Round { Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1, RoundDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        var participant = new RoundParticipant { Id = 1, PlayerId = 1 };
        var course = new Course { Id = 1, Name = "C", CourseRating = 72.0, SlopeRating = 113 };

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync((Player?)null);

        var handler = new GetPlayerScorecardQueryHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object);

        var result = await handler.Handle(new GetPlayerScorecardQuery(1, 1), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Player");
    }

    [Fact]
    public async Task Handle_WhenValid_ReturnsScorecardWithFrontBackSplit()
    {
        var round = new Round { Id = 1, SeasonId = 1, FlightId = 1, CourseId = 1, RoundDate = new DateOnly(2026, 5, 1) };
        var participant = new RoundParticipant
        {
            Id = 1, PlayerId = 1, HandicapIndex = 10, CourseHandicap = 10,
            TotalGrossStrokes = null, TotalNetStrokes = null, TotalStablefordPoints = null
        };
        var course = new Course { Id = 1, Name = "Golf Club", CourseRating = 72.0, SlopeRating = 113 };
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe" };

        var holeScores = Enumerable.Range(1, 9).Select(h => new HoleScore
        {
            HoleNumber = h, Par = 4, GrossStrokes = 4, NetStrokes = 3, StablefordPoints = 3, StrokeIndex = h, HandicapStrokes = 1
        }).ToList();

        var roundRepo = new Mock<IRoundRepository>();
        roundRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(round);
        roundRepo.Setup(r => r.GetParticipantAsync(1, 1, default)).ReturnsAsync(participant);
        roundRepo.Setup(r => r.GetHoleScoresAsync(1, default)).ReturnsAsync(holeScores);
        var courseRepo = new Mock<ICourseRepository>();
        courseRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(course);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);

        var handler = new GetPlayerScorecardQueryHandler(roundRepo.Object, courseRepo.Object, playerRepo.Object);

        var result = await handler.Handle(new GetPlayerScorecardQuery(1, 1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FrontNinePar.Should().Be(36); // 9 * 4
        result.Value.BackNinePar.Should().Be(0);    // no back nine holes
        result.Value.FrontNineGross.Should().Be(36);
    }
}
