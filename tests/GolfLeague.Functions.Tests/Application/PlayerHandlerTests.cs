using FluentAssertions;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Players.Commands;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CreatePlayerCommandHandlerTests
{
    private static Player MakePlayer(int id = 1) => new()
    {
        Id = id, FirstName = "John", LastName = "Doe", Email = "john@example.com",
        IsActive = true
    };

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsFail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByEmailAsync("j@j.com", default)).ReturnsAsync(MakePlayer());
        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new CreatePlayerCommandHandler(playerRepo.Object, handicapRepo.Object);
        var command = new CreatePlayerCommand("John", "Doe", "j@j.com", 10.0, "admin");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_WhenNew_CreatesPlayerAndHandicap()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByEmailAsync("j@j.com", default))
            .ReturnsAsync((Player?)null);

        var handicapRepo = new Mock<IHandicapRepository>();
        var handler = new CreatePlayerCommandHandler(playerRepo.Object, handicapRepo.Object);
        var command = new CreatePlayerCommand("John", "Doe", "j@j.com", 12.5, "admin");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FullName.Should().Be("John Doe");
        result.Value.CurrentHandicap.Should().Be(12.5);
        playerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Once);
        handicapRepo.Verify(r => r.AddAsync(It.Is<Handicap>(h => h.HandicapIndex == 12.5 && h.Source == HandicapSource.Initial), default), Times.Once);
    }
}

public class UpdatePlayerCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);
        var handicapRepo = new Mock<IHandicapRepository>();
        var appUserRepo = new Mock<IAppUserRepository>();
        var handler = new UpdatePlayerCommandHandler(playerRepo.Object, handicapRepo.Object, appUserRepo.Object);

        var result = await handler.Handle(new UpdatePlayerCommand(99, "J", "D", "e@e.com", "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenPlayerHasAppUser_UpdatesRoleOnAppUser()
    {
        var appUserId = Guid.NewGuid();
        var player = new Player { Id = 1, FirstName = "Old", LastName = "Name", Email = "old@e.com", FlightMemberships = [], AppUserId = appUserId };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync(new Handicap { HandicapIndex = 10.0 });
        var appUserRepo = new Mock<IAppUserRepository>();
        var handler = new UpdatePlayerCommandHandler(playerRepo.Object, handicapRepo.Object, appUserRepo.Object);

        var result = await handler.Handle(new UpdatePlayerCommand(1, "New", "Name", "new@e.com", "admin", "admin"), default);

        result.IsSuccess.Should().BeTrue();
        playerRepo.Verify(r => r.UpdateAsync(It.Is<Player>(p => p.FirstName == "New" && p.Email == "new@e.com"), default), Times.Once);
        appUserRepo.Verify(r => r.UpdateRoleAsync(appUserId, PlayerRole.Admin, default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPlayerHasNoAppUser_DoesNotCallAppUserRepo()
    {
        var player = new Player { Id = 1, FirstName = "Old", LastName = "Name", Email = "old@e.com", FlightMemberships = [], AppUserId = null };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync((Handicap?)null);
        var appUserRepo = new Mock<IAppUserRepository>();
        var handler = new UpdatePlayerCommandHandler(playerRepo.Object, handicapRepo.Object, appUserRepo.Object);

        var result = await handler.Handle(new UpdatePlayerCommand(1, "New", "Name", "new@e.com", "admin", "admin"), default);

        result.IsSuccess.Should().BeTrue();
        appUserRepo.Verify(r => r.UpdateRoleAsync(It.IsAny<Guid>(), It.IsAny<PlayerRole>(), default), Times.Never);
    }
}

public class DeactivatePlayerCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);
        var handler = new DeactivatePlayerCommandHandler(playerRepo.Object);

        var result = await handler.Handle(new DeactivatePlayerCommand(99, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenAlreadyInactive_ReturnsFail()
    {
        var player = new Player { Id = 1, IsActive = false };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handler = new DeactivatePlayerCommandHandler(playerRepo.Object);

        var result = await handler.Handle(new DeactivatePlayerCommand(1, "admin"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already inactive");
    }

    [Fact]
    public async Task Handle_WhenActive_DeactivatesAndReturnsTrue()
    {
        var player = new Player { Id = 1, IsActive = true };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handler = new DeactivatePlayerCommandHandler(playerRepo.Object);

        var result = await handler.Handle(new DeactivatePlayerCommand(1, "admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        player.IsActive.Should().BeFalse();
        playerRepo.Verify(r => r.UpdateAsync(player, default), Times.Once);
    }
}

public class SetHandicapCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);
        var handicapRepo = new Mock<IHandicapRepository>();
        var handler = new SetHandicapCommandHandler(playerRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new SetHandicapCommand(99, 10.0, null, "admin"), default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenPlayerFound_AddsHandicap()
    {
        var player = new Player { Id = 1, FirstName = "J", LastName = "D" };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        var handler = new SetHandicapCommandHandler(playerRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new SetHandicapCommand(1, 15.0, "test notes", "admin"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HandicapIndex.Should().Be(15.0);
        result.Value.Source.Should().Be(HandicapSource.Manual);
        result.Value.Notes.Should().Be("test notes");
        handicapRepo.Verify(r => r.AddAsync(It.Is<Handicap>(h => h.HandicapIndex == 15.0 && h.Source == HandicapSource.Manual), default), Times.Once);
    }
}

public class GetPlayerQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);
        var handicapRepo = new Mock<IHandicapRepository>();
        var handler = new GetPlayerQueryHandler(playerRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new GetPlayerQuery(99), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenPlayerFound_ReturnsDto()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "j@j.com", IsActive = true, FlightMemberships = [] };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync(new Handicap { HandicapIndex = 8.0 });
        var handler = new GetPlayerQueryHandler(playerRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new GetPlayerQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(1);
        result.Value.CurrentHandicap.Should().Be(8.0);
    }
}

public class GetPlayersQueryHandlerTests
{
    private static IAppUserRepository EmptyRoleRepo()
    {
        var mock = new Mock<IAppUserRepository>();
        mock.Setup(r => r.GetRolesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, PlayerRole>());
        return mock.Object;
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedPlayers()
    {
        var players = Enumerable.Range(1, 5).Select(i => new Player
        {
            Id = i, FirstName = $"F{i}", LastName = $"L{i}", Email = $"p{i}@e.com", IsActive = true, FlightMemberships = []
        }).ToList();

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetAllActiveAsync(default)).ReturnsAsync(players);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(It.IsAny<int>(), default))
            .ReturnsAsync(new Handicap { HandicapIndex = 10.0 });
        var handler = new GetPlayersQueryHandler(playerRepo.Object, handicapRepo.Object, EmptyRoleRepo());

        var result = await handler.Handle(new GetPlayersQuery(1, 3), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(3);
        result.Value.Meta.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WithActiveMembership_IncludesFlightInfoInDto()
    {
        var activeSeason = new Season { Id = 1, IsActive = true };
        var flight = new Flight { Id = 10, Name = "A Flight" };
        var membership = new FlightMembership
        {
            FlightId = 10, SeasonId = 1, Season = activeSeason, Flight = flight,
            JoinedAt = DateTime.UtcNow
        };
        var player = new Player
        {
            Id = 1, FirstName = "John", LastName = "Doe", Email = "j@j.com", IsActive = true,
            FlightMemberships = [membership]
        };

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetAllActiveAsync(default)).ReturnsAsync(new List<Player> { player });
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync((Handicap?)null);
        var handler = new GetPlayersQueryHandler(playerRepo.Object, handicapRepo.Object, EmptyRoleRepo());

        var result = await handler.Handle(new GetPlayersQuery(1, 10), default);

        result.Value!.Data[0].FlightId.Should().Be(10);
        result.Value.Data[0].FlightName.Should().Be("A Flight");
    }

    [Fact]
    public async Task Handle_WithoutActiveMembership_FlightInfoIsNull()
    {
        var player = new Player
        {
            Id = 1, FirstName = "John", LastName = "Doe", Email = "j@j.com", IsActive = true,
            FlightMemberships = []
        };

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetAllActiveAsync(default)).ReturnsAsync(new List<Player> { player });
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetCurrentAsync(1, default)).ReturnsAsync((Handicap?)null);
        var handler = new GetPlayersQueryHandler(playerRepo.Object, handicapRepo.Object, EmptyRoleRepo());

        var result = await handler.Handle(new GetPlayersQuery(1, 10), default);

        result.Value!.Data[0].FlightId.Should().BeNull();
        result.Value.Data[0].FlightName.Should().BeNull();
    }
}

public class GetHandicapHistoryQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenPlayerNotFound_ReturnsFail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((Player?)null);
        var handicapRepo = new Mock<IHandicapRepository>();
        var handler = new GetHandicapHistoryQueryHandler(playerRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new GetHandicapHistoryQuery(99), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenPlayerFound_ReturnsOrderedHistory()
    {
        var player = new Player { Id = 1, FirstName = "J", LastName = "D" };
        var handicaps = new List<Handicap>
        {
            new() { Id = 1, PlayerId = 1, HandicapIndex = 10.0, EffectiveDate = new DateOnly(2026, 1, 1), Source = HandicapSource.Initial },
            new() { Id = 2, PlayerId = 1, HandicapIndex = 9.5, EffectiveDate = new DateOnly(2026, 3, 1), Source = HandicapSource.Calculated }
        };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(player);
        var handicapRepo = new Mock<IHandicapRepository>();
        handicapRepo.Setup(r => r.GetHistoryAsync(1, default)).ReturnsAsync(handicaps);
        var handler = new GetHandicapHistoryQueryHandler(playerRepo.Object, handicapRepo.Object);

        var result = await handler.Handle(new GetHandicapHistoryQuery(1), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
        // Most recent first
        result.Value[0].EffectiveDate.Should().Be(new DateOnly(2026, 3, 1));
        result.Value[1].EffectiveDate.Should().Be(new DateOnly(2026, 1, 1));
    }
}
