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
    private static ILeagueContext MakeLeagueContext(int leagueId = 1)
    {
        var ctx = new Mock<ILeagueContext>();
        ctx.Setup(c => c.LeagueId).Returns(leagueId);
        return ctx.Object;
    }

    private static Player MakePlayer(int id = 1) => new()
    {
        Id = id,
        LeagueId = 1,
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

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object, MakeLeagueContext());
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

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object, MakeLeagueContext());
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
    public async Task Handle_CreateInvitesSkipsAlreadyLinkedPlayers()
    {
        // A player who already has an AppUser (account exists) should block the invite.
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var emailService = new Mock<IEmailService>();

        inviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var linkedPlayer = MakePlayer();
        linkedPlayer.AppUserId = Guid.NewGuid(); // already has an account
        playerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { linkedPlayer });

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object, MakeLeagueContext());
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().HaveCount(0);
        result.Value.Skipped.Should().ContainSingle("john@example.com");
    }

    [Fact]
    public async Task Handle_CreateInvitesDoesNotSkipUnlinkedPlayers()
    {
        // An unlinked player profile should not block the invite — they'll be adopted on accept.
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var emailService = new Mock<IEmailService>();

        inviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        inviteRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PlayerInvite>>(), default))
            .Returns(Task.CompletedTask);
        emailService.Setup(s => s.SendInviteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), default))
            .Returns(Task.CompletedTask);

        var unlinkedPlayer = MakePlayer(); // AppUserId = null
        playerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { unlinkedPlayer });

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object, MakeLeagueContext());
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().HaveCount(1);
        result.Value.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CreateInvitesDoesNotSkipPreLinkedPlayer()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var emailService = new Mock<IEmailService>();

        inviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var existingPlayer = MakePlayer(id: 42);
        playerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { existingPlayer });
        playerRepo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(existingPlayer);

        var handler = new CreateInvitesCommandHandler(inviteRepo.Object, playerRepo.Object, emailService.Object, MakeLeagueContext());
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player",
            42);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().HaveCount(1);
        result.Value.Skipped.Should().BeEmpty();
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

    private static Mock<ILeagueRepository> NoLeagueRepo()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.AddMembershipAsync(It.IsAny<LeagueMembership>(), default)).Returns(Task.CompletedTask);
        return repo;
    }

    private static Mock<IUserRoleService> NoExistingRoles()
    {
        var roleService = new Mock<IUserRoleService>();
        roleService.Setup(r => r.GetRolesAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(Array.Empty<string>());
        roleService.Setup(r => r.SetRolesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>>(), default))
            .ReturnsAsync(Result<bool>.Ok(true));
        return roleService;
    }

    [Fact]
    public async Task Handle_AcceptInvite_AssignsInviteRoleToAppUser()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var roleService = NoExistingRoles();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var adminInvite = MakeInvite(role: PlayerRole.Admin);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(adminInvite);

        var appUserId = Guid.NewGuid();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, NoLeagueRepo().Object, roleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "john@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        playerRepo.Verify(r => r.AddAsync(
            It.Is<Player>(p =>
                p.FirstName == "John" &&
                p.LastName == "Doe" &&
                p.Email == "john@example.com" &&
                p.AppUserId == appUserId),
            default), Times.Once);
        roleService.Verify(
            r => r.SetRolesAsync(appUserId, It.Is<IReadOnlyCollection<string>>(rs => rs.Contains("admin")), default),
            Times.Once);
        inviteRepo.Verify(r => r.UpdateAsync(
            It.Is<PlayerInvite>(i =>
                i.Status == InviteStatus.Accepted &&
                i.AcceptedByAppUserId == appUserId),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInvite_PreservesExistingRoles()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        inviteRepo.Setup(r => r.GetByTokenAsync("token-456", default))
            .ReturnsAsync(MakeInvite(role: PlayerRole.Scorer));

        var appUserId = Guid.NewGuid();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var roleService = new Mock<IUserRoleService>();
        // User already has 'player'; should end with both 'player' and 'scorer'.
        roleService.Setup(r => r.GetRolesAsync(appUserId, default))
            .ReturnsAsync(new[] { "player" });
        roleService.Setup(r => r.SetRolesAsync(appUserId, It.IsAny<IReadOnlyCollection<string>>(), default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, NoLeagueRepo().Object, roleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-456", appUserId, "Jane", "Smith", "jane@example.com", "555-1234");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        roleService.Verify(
            r => r.SetRolesAsync(
                appUserId,
                It.Is<IReadOnlyCollection<string>>(rs => rs.Contains("player") && rs.Contains("scorer")),
                default),
            Times.Once);
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
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
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
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
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
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
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
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", Guid.NewGuid(), "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task Handle_AcceptInviteAppUserAlreadyLinked_ReturnsSuccess()
    {
        // Already linked to a player with the same email — should succeed idempotently.
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());

        var appUserId = Guid.NewGuid();
        var existingPlayer = MakeLinkedPlayer(appUserId);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync(existingPlayer);
        playerRepo.Setup(r => r.GetByEmailAsync("j@e.com", default))
            .ReturnsAsync(existingPlayer);
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            new Mock<IHandicapRepository>().Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AcceptInviteAppUserAlreadyLinkedToDifferentPlayer_ReturnsSuccess()
    {
        // User is already linked to any player — always succeeds idempotently (adds role).
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());

        var appUserId = Guid.NewGuid();
        var linkedPlayer = MakeLinkedPlayer(appUserId);

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync(linkedPlayer);
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            new Mock<IHandicapRepository>().Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "J", "D", "j@e.com", null), default);

        result.IsSuccess.Should().BeTrue();
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
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "john@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
        handicapRepo.Verify(r => r.AddAsync(
            It.Is<Handicap>(h => h.HandicapIndex == 0.0 && h.Source == HandicapSource.Initial),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_AcceptInviteAdoptsPreLinkedPlayer()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var invite = MakeInvite();
        invite.PreLinkedPlayerId = 42;
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(invite);

        var appUserId = Guid.NewGuid();
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var unlinkedPlayer = new Player
        {
            Id = 42,
            FirstName = "Pre",
            LastName = "Linked",
            Email = "pre@linked.com",
            IsActive = true,
            AppUserId = null
        };
        playerRepo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(unlinkedPlayer);

        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            handicapRepo.Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "john@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
        
        // Assert that the prelinked player is updated and linked
        unlinkedPlayer.AppUserId.Should().Be(appUserId);
        unlinkedPlayer.FirstName.Should().Be("John");
        unlinkedPlayer.LastName.Should().Be("Doe");
        unlinkedPlayer.Email.Should().Be("john@example.com");

        playerRepo.Verify(r => r.UpdateAsync(unlinkedPlayer, default), Times.Once);
        playerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Never);
        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_AcceptInviteAdoptsUnlinkedPlayerByEmail()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var invite = MakeInvite();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(invite);

        var appUserId = Guid.NewGuid();
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var matchingPlayer = new Player
        {
            Id = 88,
            FirstName = "Match",
            LastName = "Email",
            Email = "john@example.com",
            IsActive = true,
            AppUserId = null
        };
        playerRepo.Setup(r => r.GetByEmailAsync("john@example.com", default))
            .ReturnsAsync(matchingPlayer);

        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            handicapRepo.Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "john@example.com", null), default);

        result.IsSuccess.Should().BeTrue();

        // Assert that the unlinked player matching email is updated and linked
        matchingPlayer.AppUserId.Should().Be(appUserId);
        matchingPlayer.FirstName.Should().Be("John");
        matchingPlayer.LastName.Should().Be("Doe");
        matchingPlayer.Email.Should().Be("john@example.com");

        playerRepo.Verify(r => r.UpdateAsync(matchingPlayer, default), Times.Once);
        playerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Never);
        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Never);
    }
}
