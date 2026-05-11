using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Players.Commands;
using GolfLeague.Application.Players.Queries;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GolfLeague.Tests;

public class PlayerFunctionsTests
{
    private static PlayerDto MakePlayerDto(int id = 1) =>
        new(id, "John Doe", "john@example.com", true, 10.0, null, null, new[] { "player" });

    private static HandicapDto MakeHandicapDto() =>
        new(1, 1, 10.0, 5.0, DateOnly.FromDateTime(DateTime.UtcNow), GolfLeague.Domain.Enums.HandicapSource.Manual, null);

    private static HttpRequest MakeRequest(string? body = null, string? role = null)
    {
        var context = new DefaultHttpContext();
        if (role is not null)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, "user1")],
                "Test");
            context.User = new ClaimsPrincipal(identity);
        }
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
        }
        return context.Request;
    }

    [Fact]
    public async Task GetPlayers_ReturnsOkResult()
    {
        var mediator = new Mock<IMediator>();
        var paged = new PagedResult<PlayerDto>([MakePlayerDto()], 1, 20, 1);
        mediator.Setup(m => m.Send(It.IsAny<GetPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<PlayerDto>>.Ok(paged));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var result = await sut.GetPlayers(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPlayer_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.GetPlayer(MakeRequest(), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPlayer_WhenNotFound_ReturnsNotFound()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Fail("Player with ID 99 not found."));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.GetPlayer(MakeRequest(), "99", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPlayer_WhenFound_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.GetPlayer(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreatePlayer_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Name = "John Doe", Email = "john@example.com", InitialHandicap = 10.0 });

        var result = await sut.CreatePlayer(MakeRequest(body), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreatePlayer_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.CreatePlayer(req, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreatePlayer_WhenAdminAndValid_ReturnsCreated()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Name = "John Doe", Email = "john@example.com", InitialHandicap = 10.0 });

        var result = await sut.CreatePlayer(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task UpdatePlayer_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { FirstName = "John", LastName = "Doe", Email = "john@example.com" });

        var result = await sut.UpdatePlayer(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdatePlayer_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { FirstName = "John", LastName = "Doe", Email = "john@example.com" });

        var result = await sut.UpdatePlayer(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePlayer_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.UpdatePlayer(req, "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePlayer_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<UpdatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { FirstName = "John", LastName = "Doe", Email = "john@example.com" });

        var result = await sut.UpdatePlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PatchPlayer_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Name = "John Doe" });

        var result = await sut.PatchPlayer(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PatchPlayer_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Name = "John Doe" });

        var result = await sut.PatchPlayer(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PatchPlayer_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.PatchPlayer(req, "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PatchPlayer_WhenPlayerNotFound_ReturnsNotFound()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GolfLeague.Domain.Entities.Player?)null);

        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Name = "John Doe" });

        var result = await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PatchPlayer_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var player = new GolfLeague.Domain.Entities.Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        mediator.Setup(m => m.Send(It.IsAny<UpdatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Name = "Jane Doe" });

        var result = await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PatchPlayer_WhenEmailProvided_UpdatesEmail()
    {
        var mediator = new Mock<IMediator>();
        var player = new GolfLeague.Domain.Entities.Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "old@example.com" };
        UpdatePlayerCommand? capturedCmd = null;
        mediator.Setup(m => m.Send(It.IsAny<UpdatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PlayerDto>>, CancellationToken>((c, _) => capturedCmd = (UpdatePlayerCommand)c)
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { Email = "new@example.com" });

        await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        capturedCmd!.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task PatchPlayer_WhenFlightIdProvided_CallsAssignToFlight()
    {
        var mediator = new Mock<IMediator>();
        var player = new GolfLeague.Domain.Entities.Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        mediator.Setup(m => m.Send(It.IsAny<UpdatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { FlightId = "2" });

        await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        playerRepo.Verify(r => r.AssignToFlightAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivatePlayerPost_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.DeactivatePlayerPost(MakeRequest(role: "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeactivatePlayerPost_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<DeactivatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Ok(true));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.DeactivatePlayerPost(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetHandicapHistory_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.GetHandicapHistory(MakeRequest(), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetHandicapHistory_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetHandicapHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<HandicapDto>>.Ok([MakeHandicapDto()]));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.GetHandicapHistory(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetHandicapPost_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { HandicapIndex = 10.0 });

        var result = await sut.SetHandicapPost(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetHandicapPost_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { HandicapIndex = 10.0 });

        var result = await sut.SetHandicapPost(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetHandicapPost_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.SetHandicapPost(req, "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetHandicapPost_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SetHandicapCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HandicapDto>.Ok(MakeHandicapDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { HandicapIndex = 10.0 });

        var result = await sut.SetHandicapPost(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetHandicap_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { HandicapIndex = 10.0 });

        var result = await sut.SetHandicap(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetHandicap_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { NewIndex = 12.0 });

        var result = await sut.SetHandicap(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetHandicap_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.SetHandicap(req, "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetHandicap_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SetHandicapCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<HandicapDto>.Ok(MakeHandicapDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);
        var body = JsonSerializer.Serialize(new { NewIndex = 12.0 });

        var result = await sut.SetHandicap(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeletePlayer_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.DeletePlayer(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeletePlayer_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.DeletePlayer(MakeRequest(role: "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeletePlayer_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<DeletePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Ok(true));

        var playerRepo = new Mock<IPlayerRepository>();
        var sut = new PlayerFunctions(mediator.Object, playerRepo.Object);

        var result = await sut.DeletePlayer(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
