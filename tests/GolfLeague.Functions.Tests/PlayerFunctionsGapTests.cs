using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Players.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Functions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GolfLeague.Tests;

/// <summary>
/// Covers PlayerFunctions endpoints that bypass MediatR (GetUnlinkedPlayers,
/// LinkPlayerToUser, SetTeeTimePreference, SetTeeTimeEmailOptOut) and the
/// flight-lock rejection branch in PatchPlayer — none of which were covered
/// by PlayerFunctionsTests.cs.
/// </summary>
public class PlayerFunctionsGapTests
{
    private static PlayerDto MakePlayerDto(int id = 1) =>
        new(id, "John Doe", "john@example.com", true, 10.0, null, null, new[] { "player" });

    private static HttpRequest MakeRequest(string? body = null, string? role = null, int? playerId = null)
    {
        var context = new DefaultHttpContext();
        if (role is not null || playerId is not null)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user1") };
            if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
            if (playerId is not null) claims.Add(new Claim("playerId", playerId.Value.ToString()));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
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

    private static PlayerFunctions MakeSut(
        Mock<IMediator>? mediator = null,
        Mock<IPlayerRepository>? playerRepo = null,
        Mock<IFlightRepository>? flightRepo = null,
        Mock<IAdminUserService>? adminUserService = null) =>
        new(
            (mediator ?? new Mock<IMediator>()).Object,
            (playerRepo ?? new Mock<IPlayerRepository>()).Object,
            (flightRepo ?? new Mock<IFlightRepository>()).Object,
            (adminUserService ?? new Mock<IAdminUserService>()).Object);

    // ── GetUnlinkedPlayers ───────────────────────────────────────────────

    [Fact]
    public async Task GetUnlinkedPlayers_WhenNotAdmin_ReturnsForbidden()
    {
        var sut = MakeSut();

        var result = await sut.GetUnlinkedPlayers(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUnlinkedPlayers_WhenAdmin_ProjectsIdNameAndEmail()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetUnlinkedActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>
            {
                new() { Id = 5, FirstName = "Jane", LastName = "Roe", Email = "jane@example.com", IsActive = true },
            });
        var sut = MakeSut(playerRepo: playerRepo);

        var result = await sut.GetUnlinkedPlayers(MakeRequest(role: "admin"), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"id\":5");
        json.Should().Contain("Jane Roe");
        json.Should().Contain("jane@example.com");
    }

    // ── LinkPlayerToUser ─────────────────────────────────────────────────

    [Fact]
    public async Task LinkPlayerToUser_WhenNotAdmin_ReturnsForbidden()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { UserId = Guid.NewGuid().ToString() });

        var result = await sut.LinkPlayerToUser(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task LinkPlayerToUser_WhenInvalidId_ReturnsBadRequest()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { UserId = Guid.NewGuid().ToString() });

        var result = await sut.LinkPlayerToUser(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LinkPlayerToUser_WhenUserIdMissingOrInvalid_ReturnsBadRequest()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { UserId = "not-a-guid" });

        var result = await sut.LinkPlayerToUser(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LinkPlayerToUser_WhenServiceFails_ReturnsConflict()
    {
        var adminUserService = new Mock<IAdminUserService>();
        adminUserService.Setup(s => s.LinkPlayerToUserAsync(1, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Fail("Player is already linked to a user account."));
        var sut = MakeSut(adminUserService: adminUserService);
        var body = JsonSerializer.Serialize(new { UserId = Guid.NewGuid().ToString() });

        var result = await sut.LinkPlayerToUser(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task LinkPlayerToUser_WhenSucceeds_ReturnsOkWithPlayer()
    {
        var userId = Guid.NewGuid();
        var adminUserService = new Mock<IAdminUserService>();
        adminUserService.Setup(s => s.LinkPlayerToUserAsync(1, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));
        var sut = MakeSut(adminUserService: adminUserService);
        var body = JsonSerializer.Serialize(new { UserId = userId.ToString() });

        var result = await sut.LinkPlayerToUser(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        adminUserService.Verify(s => s.LinkPlayerToUserAsync(1, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SetTeeTimePreference ─────────────────────────────────────────────

    [Fact]
    public async Task SetTeeTimePreference_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { PreferredSlots = new[] { "Early" } });

        var result = await sut.SetTeeTimePreference(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetTeeTimePreference_WhenDifferentPlayerAndNotAdmin_ReturnsForbidden()
    {
        // Authenticated as player 2, attempting to change player 1's preference.
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { PreferredSlots = new[] { "Early" } });

        var result = await sut.SetTeeTimePreference(MakeRequest(body, playerId: 2), "1", CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SetTeeTimePreference_WhenOwnPlayerId_Succeeds()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe" };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);
        var sut = MakeSut(playerRepo: playerRepo);
        var body = JsonSerializer.Serialize(new { PreferredSlots = new[] { "Early" } });

        var result = await sut.SetTeeTimePreference(MakeRequest(body, playerId: 1), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        playerRepo.Verify(r => r.UpdateAsync(
            It.Is<Player>(p => p.Id == 1 && p.PreferredTeeTimeSlots == GolfLeague.Domain.Enums.TeeTimeSlotPreference.Early),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTeeTimePreference_WhenAdminUpdatingOtherPlayer_Succeeds()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe" };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);
        var sut = MakeSut(playerRepo: playerRepo);
        var body = JsonSerializer.Serialize(new { PreferredSlots = new[] { "Late" } });

        // Admin role, no matching playerId claim — should still be allowed.
        var result = await sut.SetTeeTimePreference(MakeRequest(body, role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetTeeTimePreference_WhenPlayerNotFound_ReturnsNotFound()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);
        var sut = MakeSut(playerRepo: playerRepo);
        var body = JsonSerializer.Serialize(new { PreferredSlots = new[] { "Early" } });

        var result = await sut.SetTeeTimePreference(MakeRequest(body, playerId: 1), "1", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetTeeTimePreference_WhenEmptySlots_SetsNone()
    {
        var player = new Player
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            PreferredTeeTimeSlots = GolfLeague.Domain.Enums.TeeTimeSlotPreference.Early,
        };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);
        var sut = MakeSut(playerRepo: playerRepo);
        var body = JsonSerializer.Serialize(new { PreferredSlots = Array.Empty<string>() });

        await sut.SetTeeTimePreference(MakeRequest(body, playerId: 1), "1", CancellationToken.None);

        player.PreferredTeeTimeSlots.Should().Be(GolfLeague.Domain.Enums.TeeTimeSlotPreference.None);
    }

    // ── SetTeeTimeEmailOptOut ────────────────────────────────────────────

    [Fact]
    public async Task SetTeeTimeEmailOptOut_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { OptOut = true });

        var result = await sut.SetTeeTimeEmailOptOut(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetTeeTimeEmailOptOut_WhenDifferentPlayer_ReturnsForbidden_EvenForAdmin()
    {
        // Unlike SetTeeTimePreference, this endpoint only allows the player
        // themselves — being an admin does not bypass the check.
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { OptOut = true });

        var result = await sut.SetTeeTimeEmailOptOut(MakeRequest(body, role: "admin", playerId: 2), "1", CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SetTeeTimeEmailOptOut_WhenOwnPlayerId_Succeeds()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe", TeeTimeEmailOptOut = false };
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);
        var sut = MakeSut(playerRepo: playerRepo);
        var body = JsonSerializer.Serialize(new { OptOut = true });

        var result = await sut.SetTeeTimeEmailOptOut(MakeRequest(body, playerId: 1), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        player.TeeTimeEmailOptOut.Should().BeTrue();
    }

    [Fact]
    public async Task SetTeeTimeEmailOptOut_WhenPlayerNotFound_ReturnsNotFound()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);
        var sut = MakeSut(playerRepo: playerRepo);
        var body = JsonSerializer.Serialize(new { OptOut = true });

        var result = await sut.SetTeeTimeEmailOptOut(MakeRequest(body, playerId: 1), "1", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── PatchPlayer: flight-lock rejection branch ───────────────────────

    [Fact]
    public async Task PatchPlayer_WhenTargetFlightHalfIsLocked_ReturnsConflict()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        var flight = new Flight { Id = 2, HalfId = 9 };

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(flight);
        flightRepo.Setup(r => r.IsHalfLockedAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = MakeSut(playerRepo: playerRepo, flightRepo: flightRepo);
        var body = JsonSerializer.Serialize(new { FlightId = "2" });

        var result = await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(409);
        playerRepo.Verify(r => r.AssignToFlightAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PatchPlayer_WhenTargetFlightHalfIsNotLocked_AssignsFlight()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        var flight = new Flight { Id = 2, HalfId = 9 };
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<UpdatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(flight);
        flightRepo.Setup(r => r.IsHalfLockedAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = MakeSut(mediator: mediator, playerRepo: playerRepo, flightRepo: flightRepo);
        var body = JsonSerializer.Serialize(new { FlightId = "2" });

        var result = await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        playerRepo.Verify(r => r.AssignToFlightAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatchPlayer_WhenFlightIdClearedToEmpty_UnassignsWithoutLockCheck()
    {
        var player = new Player { Id = 1, FirstName = "John", LastName = "Doe", Email = "john@example.com" };
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<UpdatePlayerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(player);
        var flightRepo = new Mock<IFlightRepository>();

        var sut = MakeSut(mediator: mediator, playerRepo: playerRepo, flightRepo: flightRepo);
        var body = JsonSerializer.Serialize(new { FlightId = "" });

        var result = await sut.PatchPlayer(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        flightRepo.Verify(r => r.IsHalfLockedAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        playerRepo.Verify(r => r.AssignToFlightAsync(1, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── SetPlayerHalfMembership ──────────────────────────────────────────

    [Fact]
    public async Task SetPlayerHalfMembership_WhenNotAdmin_ReturnsForbidden()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { HalfId = 1, FlightId = 2 });

        var result = await sut.SetPlayerHalfMembership(MakeRequest(body), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetPlayerHalfMembership_WhenInvalidId_ReturnsBadRequest()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { HalfId = 1, FlightId = 2 });

        var result = await sut.SetPlayerHalfMembership(MakeRequest(body, "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetPlayerHalfMembership_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SetHalfMembershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlayerDto>.Ok(MakePlayerDto()));
        var sut = MakeSut(mediator: mediator);
        var body = JsonSerializer.Serialize(new { HalfId = 1, FlightId = 2 });

        var result = await sut.SetPlayerHalfMembership(MakeRequest(body, "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
