using FluentAssertions;
using GolfLeague.Application.Flights.Commands;
using GolfLeague.Application.Flights.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CreateFlightCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenSeasonIdProvided_CreatesFlight()
    {
        var repo = new Mock<IFlightRepository>();
        var handler = new CreateFlightCommandHandler(repo.Object);
        var cmd = new CreateFlightCommand("A Flight", 1, 0.0, 18.0, 1, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("A Flight");
        result.Value.SeasonId.Should().Be(1);
        repo.Verify(r => r.AddAsync(It.IsAny<Flight>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoSeasonIdAndNoActiveSeason_ReturnsFail()
    {
        var repo = new Mock<IFlightRepository>();
        repo.Setup(r => r.GetActiveSeasonIdAsync(default)).ReturnsAsync((int?)null);
        var handler = new CreateFlightCommandHandler(repo.Object);
        var cmd = new CreateFlightCommand("A Flight", null, null, null, 1, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No active season");
    }

    [Fact]
    public async Task Handle_WhenNoSeasonIdButActiveSeasonExists_UseActiveSeason()
    {
        var repo = new Mock<IFlightRepository>();
        repo.Setup(r => r.GetActiveSeasonIdAsync(default)).ReturnsAsync(5);
        var handler = new CreateFlightCommandHandler(repo.Object);
        var cmd = new CreateFlightCommand("A Flight", null, null, null, 1, "admin");

        var result = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SeasonId.Should().Be(5);
    }
}

public class GetFlightsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsMappedDtos()
    {
        var flights = new List<Flight>
        {
            new() { Id = 1, SeasonId = 1, Name = "A Flight", MinHandicap = 0, MaxHandicap = 18, DisplayOrder = 1, Memberships = [] }
        };
        var repo = new Mock<IFlightRepository>();
        repo.Setup(r => r.GetAllAsync(default)).ReturnsAsync(flights);
        var handler = new GetFlightsQueryHandler(repo.Object);

        var result = await handler.Handle(new GetFlightsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].Name.Should().Be("A Flight");
    }
}

public class GetFlightStandingsQueryHandlerTests
{
    private static IReadOnlyList<RoundParticipant> MakeParticipants(params RoundParticipant[] items)
        => items.ToList().AsReadOnly();

    [Fact]
    public async Task Handle_WhenFlightNotFound_ReturnsFail()
    {
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Flight?)null);
        var handler = new GetFlightStandingsQueryHandler(flightRepo.Object, Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>());

        var result = await handler.Handle(new GetFlightStandingsQuery(99, 1), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenFlightFound_ReturnsRankedStandings()
    {
        var flight = new Flight { Id = 1, Name = "A Flight" };
        var player1 = new Player { Id = 1, FirstName = "Alice", LastName = "Smith", FlightMemberships = [] };
        var player2 = new Player { Id = 2, FirstName = "Bob", LastName = "Jones", FlightMemberships = [] };

        var participants = MakeParticipants(
            new RoundParticipant { PlayerId = 1, IsWithdrawn = false, TotalStablefordPoints = 30 },
            new RoundParticipant { PlayerId = 1, IsWithdrawn = false, TotalStablefordPoints = 25 },
            new RoundParticipant { PlayerId = 2, IsWithdrawn = false, TotalStablefordPoints = 35 });

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetStandingsAsync(1, 1, default)).ReturnsAsync(participants);

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player1);
        playerRepo.Setup(r => r.GetByIdAsync(2, default)).ReturnsAsync(player2);

        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new Handicap { HandicapIndex = 10.0 });

        var handler = new GetFlightStandingsQueryHandler(flightRepo.Object, handicapRepo.Object, playerRepo.Object);

        var result = await handler.Handle(new GetFlightStandingsQuery(1, 1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        result.Value[0].PlayerFullName.Should().Be("Alice Smith");
        result.Value[0].Position.Should().Be(1);
        result.Value[1].PlayerFullName.Should().Be("Bob Jones");
        result.Value[1].Position.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenPlayerNotFound_SkipsParticipant()
    {
        var flight = new Flight { Id = 1, Name = "A Flight" };
        var participants = MakeParticipants(
            new RoundParticipant { PlayerId = 99, IsWithdrawn = false, TotalStablefordPoints = 30 });

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetStandingsAsync(1, 1, default)).ReturnsAsync(participants);

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);

        var handler = new GetFlightStandingsQueryHandler(flightRepo.Object, Mock.Of<IHandicapRepository>(), playerRepo.Object);

        var result = await handler.Handle(new GetFlightStandingsQuery(1, 1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SkipsWithdrawnParticipants()
    {
        var flight = new Flight { Id = 1, Name = "A Flight" };
        var participants = MakeParticipants(
            new RoundParticipant { PlayerId = 1, IsWithdrawn = true, TotalStablefordPoints = 30 });

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(flight);
        flightRepo.Setup(r => r.GetStandingsAsync(1, 1, default)).ReturnsAsync(participants);

        var handler = new GetFlightStandingsQueryHandler(flightRepo.Object, Mock.Of<IHandicapRepository>(), Mock.Of<IPlayerRepository>());

        var result = await handler.Handle(new GetFlightStandingsQuery(1, 1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }
}
