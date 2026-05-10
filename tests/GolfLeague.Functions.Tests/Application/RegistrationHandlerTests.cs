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
    private static Player MakePlayer(int id = 1) => new()
    {
        Id = id,
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        IsActive = true,
    };

    [Fact]
    public async Task Handle_CreateInvitesWithAdminRole_SetsRoleOnInvite()
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
            "http://localhost:5173",
            7,
            "admin");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().HaveCount(1);
        result.Value.Created[0].Role.Should().Be("admin");

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
    private static PlayerInvite MakeInvite(InviteStatus status = InviteStatus.Pending, PlayerRole role = PlayerRole.Player) => new()
    {
        Id = 1,
        Email = "test@example.com",
        Token = "token-123",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        InvitedByUserId = "admin-1",
        Role = role
    };

    private static Player MakeLinkedPlayer(Guid appUserId) => new()
    {
        Id = 1,
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        IsActive = true,
        AppUserId = appUserId,
    };

    [Fact]
    public async Task Handle_AcceptInvite_CreatesPlayerWithInviteRole()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var appUserRepo = new Mock<IAppUserRepository>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var adminInvite = MakeInvite(role: PlayerRole.Admin);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(adminInvite);

        var appUserId = Guid.NewGuid();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, appUserRepo.Object, logger.Object);
        var command = new AcceptInviteCommand(
            "token-123",
            appUserId,
            "John",
            "Doe",
            "john@example.com",
            null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("admin");

        playerRepo.Verify(r => r.AddAsync(
            It.Is<Player>(p =>
                p.FirstName == "John" &&
                p.LastName == "Doe" &&
                p.Email == "john@example.com" &&
                p.AppUserId == appUserId),
            default), Times.Once);

        appUserRepo.Verify(r => r.UpdateRoleAsync(appUserId, PlayerRole.Admin, default), Times.Once);

        inviteRepo.Verify(r => r.UpdateAsync(
            It.Is<PlayerInvite>(i =>
                i.Status == InviteStatus.Accepted &&
                i.AcceptedByAppUserId == appUserId),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInviteWithScorerRole_AssignsScorer()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var appUserRepo = new Mock<IAppUserRepository>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        inviteRepo.Setup(r => r.GetByTokenAsync("token-456", default))
            .ReturnsAsync(MakeInvite(role: PlayerRole.Scorer));

        var appUserId = Guid.NewGuid();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, appUserRepo.Object, logger.Object);
        var command = new AcceptInviteCommand("token-456", appUserId, "Jane", "Smith", "jane@example.com", "555-1234");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be("scorer");
        appUserRepo.Verify(r => r.UpdateRoleAsync(appUserId, PlayerRole.Scorer, default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInviteNotFound_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("invalid-token", default))
            .ReturnsAsync((PlayerInvite?)null);

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            new Mock<IPlayerRepository>().Object,
            new Mock<IHandicapRepository>().Object,
            new Mock<IAppUserRepository>().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("invalid-token", Guid.NewGuid(), "John", "Doe", "j@e.com", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invite not found");
    }

    [Fact]
    public async Task Handle_AcceptInviteRevoked_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite(status: InviteStatus.Revoked));

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            new Mock<IPlayerRepository>().Object,
            new Mock<IHandicapRepository>().Object,
            new Mock<IAppUserRepository>().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", Guid.NewGuid(), "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("revoked");
    }

    [Fact]
    public async Task Handle_AcceptInviteAlreadyAccepted_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite(status: InviteStatus.Accepted));

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            new Mock<IPlayerRepository>().Object,
            new Mock<IHandicapRepository>().Object,
            new Mock<IAppUserRepository>().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", Guid.NewGuid(), "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already been used");
    }

    [Fact]
    public async Task Handle_AcceptInviteExpired_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var expired = new PlayerInvite
        {
            Id = 1, Email = "t@e.com", Token = "token-123",
            Status = InviteStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            InvitedByUserId = "admin-1",
            Role = PlayerRole.Player
        };
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(expired);

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            new Mock<IPlayerRepository>().Object,
            new Mock<IHandicapRepository>().Object,
            new Mock<IAppUserRepository>().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", Guid.NewGuid(), "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_AcceptInviteAppUserAlreadyLinked_ReturnsFail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());

        var appUserId = Guid.NewGuid();
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync(MakeLinkedPlayer(appUserId));

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            new Mock<IHandicapRepository>().Object,
            new Mock<IAppUserRepository>().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already linked");
    }

    [Fact]
    public async Task Handle_AcceptInviteCreatesHandicapRecord()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());

        var appUserId = Guid.NewGuid();
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            handicapRepo.Object,
            new Mock<IAppUserRepository>().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "john@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
        handicapRepo.Verify(r => r.AddAsync(
            It.Is<Handicap>(h => h.HandicapIndex == 0.0 && h.Source == HandicapSource.Initial),
            default), Times.Once);
    }
}
