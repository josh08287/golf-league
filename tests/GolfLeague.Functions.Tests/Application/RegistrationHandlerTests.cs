using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Registrations.Commands;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class CreateInvitesCommandHandlerTests
{
    private static PlayerInvite MakeInvite(int id = 1, InviteStatus status = InviteStatus.Pending, PlayerRole role = PlayerRole.Player) => new()
    {
        Id = id,
        Email = "test@example.com",
        Token = "token-123",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        InvitedByUserId = "admin-1",
        Role = role
    };

    private static Player MakePlayer(int id = 1) => new()
    {
        Id = id,
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        EntraObjectId = "entra-1",
        IsActive = true,
        Role = PlayerRole.Player
    };

    [Fact]
    public async Task Handle_CreateInvitesWithAdminRole_SetsRoleOnInvite()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var emailService = new Mock<IEmailService>();

        // Setup: no pending invite exists
        inviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        // Setup: player doesn't exist
        playerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player>());

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object);
        var command = new CreateInvitesCommand(
            new[] { "new@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "admin");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().HaveCount(1);
        result.Value.Created[0].Role.Should().Be("admin");

        // Verify invite was added with admin role
        inviteRepo.Verify(r => r.AddRangeAsync(
            It.Is<List<PlayerInvite>>(invites =>
                invites.Count == 1 &&
                invites[0].Email == "new@example.com" &&
                invites[0].Role == PlayerRole.Admin),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_CreateInvitesWithoutRole_DefaultsToPlayer()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var emailService = new Mock<IEmailService>();

        inviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        playerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player>());

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object);
        var command = new CreateInvitesCommand(
            new[] { "new@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created[0].Role.Should().Be("player");

        // Verify invite was added with player role
        inviteRepo.Verify(r => r.AddRangeAsync(
            It.Is<List<PlayerInvite>>(invites =>
                invites[0].Role == PlayerRole.Player),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_CreateInvitesSkipsExistingPlayers()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var emailService = new Mock<IEmailService>();

        inviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        // Setup: existing player with this email
        var existingPlayer = MakePlayer();
        playerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { existingPlayer });

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object);
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().HaveCount(0);
        result.Value.Skipped.Should().ContainSingle("john@example.com");
    }
}

public class AcceptInviteCommandHandlerTests
{
    private static PlayerInvite MakeInvite(int id = 1, InviteStatus status = InviteStatus.Pending, PlayerRole role = PlayerRole.Player) => new()
    {
        Id = id,
        Email = "test@example.com",
        Token = "token-123",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        InvitedByUserId = "admin-1",
        Role = role
    };

    private static Player MakePlayer(int id = 1, PlayerRole role = PlayerRole.Player) => new()
    {
        Id = id,
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        EntraObjectId = "entra-1",
        IsActive = true,
        Role = role
    };

    [Fact]
    public async Task Handle_AcceptInvite_CreatesPlayerWithInviteRole()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var adminInvite = MakeInvite(role: PlayerRole.Admin);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(adminInvite);

        playerRepo.Setup(r => r.GetByEntraObjectIdAsync("entra-new", default))
            .ReturnsAsync((Player?)null);

        entraRoleService.Setup(s => s.EnsureUserExistsAsync("john@example.com", "John Doe", default))
            .ReturnsAsync(Result<string>.Ok("entra-new"));
        entraRoleService.Setup(s => s.AssignRoleAsync("entra-new", "admin", default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand(
            "token-123",
            "entra-new",
            "John",
            "Doe",
            "john@example.com",
            null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("admin");

        // Verify player was created with admin role from invite
        playerRepo.Verify(r => r.AddAsync(
            It.Is<Player>(p =>
                p.FirstName == "John" &&
                p.LastName == "Doe" &&
                p.Email == "john@example.com" &&
                p.EntraObjectId == "entra-new" &&
                p.Role == PlayerRole.Admin),
            default), Times.Once);

        // Verify invite marked as accepted
        inviteRepo.Verify(r => r.UpdateAsync(
            It.Is<PlayerInvite>(i =>
                i.Status == InviteStatus.Accepted &&
                i.AcceptedByEntraObjectId == "entra-new"),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInviteWithScorerRole_CreatesPlayerWithScorerRole()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var scorerInvite = MakeInvite(role: PlayerRole.Scorer);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-456", default))
            .ReturnsAsync(scorerInvite);

        playerRepo.Setup(r => r.GetByEntraObjectIdAsync("entra-scorer", default))
            .ReturnsAsync((Player?)null);

        entraRoleService.Setup(s => s.EnsureUserExistsAsync("jane@example.com", "Jane Smith", default))
            .ReturnsAsync(Result<string>.Ok("entra-scorer"));
        entraRoleService.Setup(s => s.AssignRoleAsync("entra-scorer", "scorer", default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand(
            "token-456",
            "entra-scorer",
            "Jane",
            "Smith",
            "jane@example.com",
            "555-1234");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("scorer");

        playerRepo.Verify(r => r.AddAsync(
            It.Is<Player>(p => p.Role == PlayerRole.Scorer),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInviteWithPlayerRole_CreatesPlayerWithPlayerRole()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var playerInvite = MakeInvite(role: PlayerRole.Player);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-789", default))
            .ReturnsAsync(playerInvite);

        playerRepo.Setup(r => r.GetByEntraObjectIdAsync("entra-player", default))
            .ReturnsAsync((Player?)null);

        entraRoleService.Setup(s => s.EnsureUserExistsAsync("bob@example.com", "Bob Johnson", default))
            .ReturnsAsync(Result<string>.Ok("entra-player"));
        entraRoleService.Setup(s => s.AssignRoleAsync("entra-player", "player", default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand(
            "token-789",
            "entra-player",
            "Bob",
            "Johnson",
            "bob@example.com",
            null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("player");

        playerRepo.Verify(r => r.AddAsync(
            It.Is<Player>(p => p.Role == PlayerRole.Player),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInviteNotFound_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        inviteRepo.Setup(r => r.GetByTokenAsync("invalid-token", default))
            .ReturnsAsync((PlayerInvite?)null);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand("invalid-token", "entra-1", "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invite not found");
    }

    [Fact]
    public async Task Handle_AcceptInviteRevoked_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var revokedInvite = MakeInvite(status: InviteStatus.Revoked);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(revokedInvite);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", "entra-1", "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("revoked");
    }

    [Fact]
    public async Task Handle_AcceptInviteAlreadyAccepted_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var acceptedInvite = MakeInvite(status: InviteStatus.Accepted);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(acceptedInvite);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", "entra-1", "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already been used");
    }

    [Fact]
    public async Task Handle_AcceptInviteExpired_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var expiredInvite = new PlayerInvite
        {
            Id = 1,
            Email = "test@example.com",
            Token = "token-123",
            Status = InviteStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),  // Expired
            InvitedByUserId = "admin-1",
            Role = PlayerRole.Player
        };

        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(expiredInvite);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", "entra-1", "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_AcceptInviteEntraIdAlreadyExists_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var validInvite = MakeInvite();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(validInvite);

        // Setup: Entra ID already linked to another player
        var existingPlayer = MakePlayer();
        playerRepo.Setup(r => r.GetByEntraObjectIdAsync("entra-1", default))
            .ReturnsAsync(existingPlayer);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", "entra-1", "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already linked");
    }

    [Fact]
    public async Task Handle_AcceptInviteCreatesHandicapRecord()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var entraRoleService = new Mock<IEntraRoleService>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var invite = MakeInvite();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(invite);

        playerRepo.Setup(r => r.GetByEntraObjectIdAsync("entra-new", default))
            .ReturnsAsync((Player?)null);

        entraRoleService.Setup(s => s.EnsureUserExistsAsync("john@example.com", "John Doe", default))
            .ReturnsAsync(Result<string>.Ok("entra-new"));
        entraRoleService.Setup(s => s.AssignRoleAsync("entra-new", "player", default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, entraRoleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", "entra-new", "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();

        // Verify handicap was created
        handicapRepo.Verify(r => r.AddAsync(
            It.Is<Handicap>(h =>
                h.HandicapIndex == 0.0 &&
                h.Source == HandicapSource.Initial),
            default), Times.Once);
    }
}
