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

    private sealed class Mocks
    {
        public Mock<IInviteRepository> InviteRepo { get; } = new();
        public Mock<IPlayerRepository> PlayerRepo { get; } = new();
        public Mock<IAppUserRepository> AppUserRepo { get; } = new();
        public Mock<ILeagueRepository> LeagueRepo { get; } = new();
        public Mock<IHandicapRepository> HandicapRepo { get; } = new();
        public Mock<IUserRoleService> RoleService { get; } = new();
        public Mock<IEmailService> EmailService { get; } = new();

        public Mocks()
        {
            // Default: no existing account for any email (most tests cover the brand-new-invite path).
            AppUserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                .ReturnsAsync((AppUser?)null);
            InviteRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PlayerInvite>>(), default))
                .Returns(Task.CompletedTask);
            InviteRepo.Setup(r => r.AddAsync(It.IsAny<PlayerInvite>(), default))
                .Returns(Task.CompletedTask);
            EmailService.Setup(s => s.SendInviteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), default))
                .Returns(Task.CompletedTask);
        }

        public CreateInvitesCommandHandler BuildHandler(int leagueId = 1) => new(
            InviteRepo.Object,
            PlayerRepo.Object,
            AppUserRepo.Object,
            LeagueRepo.Object,
            HandicapRepo.Object,
            RoleService.Object,
            EmailService.Object,
            MakeLeagueContext(leagueId));
    }

    [Fact]
    public async Task Handle_CreateInvitesWithAdminRole_SetsRoleOnInvite()
    {
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player>());

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "new@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "admin");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Created.Should().HaveCount(1);
        result.Value!.Created[0].Role.Should().Be("admin");

        m.InviteRepo.Verify(r => r.AddRangeAsync(
            It.Is<List<PlayerInvite>>(invites =>
                invites.Count == 1 &&
                invites[0].Email == "new@example.com" &&
                invites[0].Role == PlayerRole.Admin),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_CreateInvitesWithoutRole_DefaultsToPlayer()
    {
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player>());

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "new@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Created[0].Role.Should().Be("player");

        m.InviteRepo.Verify(r => r.AddRangeAsync(
            It.Is<List<PlayerInvite>>(invites =>
                invites[0].Role == PlayerRole.Player),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_CreateInvitesSkipsAlreadyLinkedPlayers()
    {
        // A player who already has an AppUser (account exists) should block the invite.
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var linkedPlayer = MakePlayer();
        linkedPlayer.AppUserId = Guid.NewGuid(); // already has an account
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { linkedPlayer });

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Created.Should().HaveCount(0);
        result.Value!.Skipped.Should().ContainSingle("john@example.com");
    }

    [Fact]
    public async Task Handle_CreateInvitesDoesNotSkipUnlinkedPlayers()
    {
        // An unlinked player profile should not block the invite — they'll be adopted on accept.
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var unlinkedPlayer = MakePlayer(); // AppUserId = null
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { unlinkedPlayer });

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Created.Should().HaveCount(1);
        result.Value!.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CreateInvitesDoesNotSkipPreLinkedPlayer()
    {
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var existingPlayer = MakePlayer(id: 42);
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { existingPlayer });
        m.PlayerRepo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(existingPlayer);

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player",
            42);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Created.Should().HaveCount(1);
        result.Value!.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PreLinkedPlayerWithNoEmail_BackfillsInviteEmailImmediately()
    {
        // A pre-linked player with no email configured should show the
        // invite's email on their profile right away, not just after accept.
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var noEmailPlayer = MakePlayer(id: 42);
        noEmailPlayer.Email = null;
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { noEmailPlayer });
        m.PlayerRepo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(noEmailPlayer);
        m.PlayerRepo.Setup(r => r.UpdateAsync(It.IsAny<Player>(), default))
            .Returns(Task.CompletedTask);

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "john@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player",
            42);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        noEmailPlayer.Email.Should().Be("john@example.com");
        m.PlayerRepo.Verify(r => r.UpdateAsync(
            It.Is<Player>(p => p.Id == 42 && p.Email == "john@example.com"),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_PreLinkedPlayerWithExistingEmail_DoesNotOverwriteOnInviteCreation()
    {
        // If the admin already set an email on the player profile, creating
        // an invite for a different address must not clobber it — only the
        // acceptance flow should replace it, with the account's own email.
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var existingEmailPlayer = MakePlayer(id: 42); // Email = "john@example.com"
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player> { existingEmailPlayer });
        m.PlayerRepo.Setup(r => r.GetByIdAsync(42, default))
            .ReturnsAsync(existingEmailPlayer);

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "someone-else@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player",
            42);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        existingEmailPlayer.Email.Should().Be("john@example.com");
        m.PlayerRepo.Verify(r => r.UpdateAsync(It.IsAny<Player>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailBelongsToExistingAppUser_AutoLinksWithoutSendingEmail()
    {
        // The invited email already has a login (e.g. member of another league).
        // There's nothing for them to "accept" — link immediately, send no email.
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player>());

        var existingUser = new AppUser { Id = Guid.NewGuid(), Email = "existing@example.com", UserName = "existing@example.com" };
        m.AppUserRepo.Setup(r => r.GetByEmailAsync("existing@example.com", default))
            .ReturnsAsync(existingUser);

        var otherLeagueProfile = new Player { Id = 99, LeagueId = 2, FirstName = "Jane", LastName = "Roe", AppUserId = existingUser.Id, IsActive = true };
        m.PlayerRepo.Setup(r => r.GetAllByAppUserIdAsync(existingUser.Id, default))
            .ReturnsAsync(new List<Player> { otherLeagueProfile });

        m.PlayerRepo.Setup(r => r.AddAsync(It.IsAny<Player>(), default))
            .Callback<Player, CancellationToken>((p, _) => p.Id = 7)
            .Returns(Task.CompletedTask);

        m.RoleService.Setup(r => r.GetRolesAsync(existingUser.Id, default))
            .ReturnsAsync(Array.Empty<string>());
        m.RoleService.Setup(r => r.SetRolesAsync(existingUser.Id, It.IsAny<IReadOnlyCollection<string>>(), default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = m.BuildHandler(leagueId: 1);
        var command = new CreateInvitesCommand(
            new[] { "existing@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Created.Should().BeEmpty();
        result.Value!.Skipped.Should().BeEmpty();
        result.Value!.AutoLinked.Should().ContainSingle("existing@example.com");

        // No invite email sent — there's nothing to accept by email.
        m.EmailService.Verify(s => s.SendInviteAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), default), Times.Never);

        // A new Player is created in this league, copying the name from the other-league profile.
        m.PlayerRepo.Verify(r => r.AddAsync(
            It.Is<Player>(p =>
                p.LeagueId == 1 &&
                p.AppUserId == existingUser.Id &&
                p.FirstName == "Jane" &&
                p.LastName == "Roe" &&
                p.Email == "existing@example.com"),
            default), Times.Once);

        // League membership is granted for this league.
        m.LeagueRepo.Verify(r => r.AddMembershipAsync(
            It.Is<LeagueMembership>(lm =>
                lm.LeagueId == 1 &&
                lm.UserId == existingUser.Id &&
                lm.Role == PlayerRole.Player),
            default), Times.Once);

        // Role is granted on the AppUser.
        m.RoleService.Verify(r => r.SetRolesAsync(
            existingUser.Id,
            It.Is<IReadOnlyCollection<string>>(rs => rs.Contains("player")),
            default), Times.Once);

        // An already-Accepted invite is recorded for audit history.
        m.InviteRepo.Verify(r => r.AddAsync(
            It.Is<PlayerInvite>(i =>
                i.Email == "existing@example.com" &&
                i.LeagueId == 1 &&
                i.Status == InviteStatus.Accepted &&
                i.AcceptedByAppUserId == existingUser.Id &&
                i.PlayerId == 7),
            default), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailBelongsToExistingAppUser_DoesNotDuplicateRoleAlreadyHeld()
    {
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default))
            .ReturnsAsync(new List<Player>());

        var existingUser = new AppUser { Id = Guid.NewGuid(), Email = "existing@example.com" };
        m.AppUserRepo.Setup(r => r.GetByEmailAsync("existing@example.com", default))
            .ReturnsAsync(existingUser);
        m.PlayerRepo.Setup(r => r.GetAllByAppUserIdAsync(existingUser.Id, default))
            .ReturnsAsync(new List<Player>());
        m.PlayerRepo.Setup(r => r.AddAsync(It.IsAny<Player>(), default))
            .Returns(Task.CompletedTask);

        // Already a "player" elsewhere — should not call SetRolesAsync again.
        m.RoleService.Setup(r => r.GetRolesAsync(existingUser.Id, default))
            .ReturnsAsync(new[] { "player" });

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "existing@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player");

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AutoLinked.Should().ContainSingle("existing@example.com");
        m.RoleService.Verify(r => r.SetRolesAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<string>>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_PreLinkedPlayerForExistingAppUser_AdoptsPreLinkedPlayerInstedOfCreatingNew()
    {
        var m = new Mocks();
        m.InviteRepo.Setup(r => r.PendingInviteExistsForEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var prelink = MakePlayer(id: 55);
        prelink.Email = "existing@example.com";
        m.PlayerRepo.Setup(r => r.GetByIdAsync(55, default)).ReturnsAsync(prelink);
        m.PlayerRepo.Setup(r => r.GetAllActiveAsync(default)).ReturnsAsync(new List<Player> { prelink });
        m.PlayerRepo.Setup(r => r.UpdateAsync(It.IsAny<Player>(), default)).Returns(Task.CompletedTask);

        var existingUser = new AppUser { Id = Guid.NewGuid(), Email = "existing@example.com" };
        m.AppUserRepo.Setup(r => r.GetByEmailAsync("existing@example.com", default))
            .ReturnsAsync(existingUser);
        m.RoleService.Setup(r => r.GetRolesAsync(existingUser.Id, default))
            .ReturnsAsync(Array.Empty<string>());
        m.RoleService.Setup(r => r.SetRolesAsync(existingUser.Id, It.IsAny<IReadOnlyCollection<string>>(), default))
            .ReturnsAsync(Result<bool>.Ok(true));

        var handler = m.BuildHandler();
        var command = new CreateInvitesCommand(
            new[] { "existing@example.com" },
            "admin-1",
            "http://localhost:5173",
            7,
            "player",
            55);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AutoLinked.Should().ContainSingle("existing@example.com");

        m.PlayerRepo.Verify(r => r.UpdateAsync(
            It.Is<Player>(p => p.Id == 55 && p.AppUserId == existingUser.Id),
            default), Times.Once);
        m.PlayerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Never);
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
        // No pre-linked player: invite is accepted, role is granted, membership
        // is created, but no player profile is created.
        var inviteRepo = new Mock<IInviteRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var handicapRepo = new Mock<IHandicapRepository>();
        var roleService = NoExistingRoles();
        var logger = new Mock<ILogger<AcceptInviteCommandHandler>>();

        var adminInvite = MakeInvite(role: PlayerRole.Admin);
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(adminInvite);
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var appUserId = Guid.NewGuid();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var handler = new AcceptInviteCommandHandler(inviteRepo.Object, playerRepo.Object, handicapRepo.Object, NoLeagueRepo().Object, roleService.Object, logger.Object);
        var command = new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "test@example.com", null);

        var result = await handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        playerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Never);
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

        var invite = MakeInvite(role: PlayerRole.Scorer); // Email = "test@example.com"
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(invite);
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

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
        var command = new AcceptInviteCommand("token-123", appUserId, "Jane", "Smith", "test@example.com", "555-1234");

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
        // User already has a linked player — should succeed idempotently (reuses existing player).
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var appUserId = Guid.NewGuid();
        var existingPlayer = MakeLinkedPlayer(appUserId);
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync(existingPlayer);

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            new Mock<IHandicapRepository>().Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "J", "D", "test@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AcceptInviteAppUserAlreadyLinkedToDifferentPlayer_ReturnsSuccess()
    {
        // User already has a linked player (different from any pre-link) — succeeds idempotently.
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var appUserId = Guid.NewGuid();
        var linkedPlayer = MakeLinkedPlayer(appUserId);

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync(linkedPlayer);

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            new Mock<IHandicapRepository>().Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "J", "D", "test@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AcceptInviteWithPreLink_CreatesHandicapRecord()
    {
        // A pre-linked player triggers the full link path, which seeds a handicap
        // when none exists yet.
        var inviteRepo = new Mock<IInviteRepository>();
        var invite = MakeInvite();
        invite.PreLinkedPlayerId = 99;
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(invite);
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var appUserId = Guid.NewGuid();
        var unlinkedPlayer = new Player
        {
            Id = 99,
            FirstName = "Pre",
            LastName = "Linked",
            Email = "test@example.com",
            IsActive = true,
            AppUserId = null,
        };

        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default)).ReturnsAsync((Player?)null);
        playerRepo.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync(unlinkedPlayer);

        var handicapRepo = new Mock<IHandicapRepository>();

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            handicapRepo.Object,
            NoLeagueRepo().Object,
            NoExistingRoles().Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "test@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
        handicapRepo.Verify(r => r.AddAsync(
            It.Is<Handicap>(h => h.HandicapIndex == 0.0 && h.Source == HandicapSource.Initial),
            default), Times.Never);
    }

    [Fact]
    public async Task Handle_AcceptInviteAdoptsPreLinkedPlayer()
    {
        var inviteRepo = new Mock<IInviteRepository>();
        var invite = MakeInvite(); // Email = "test@example.com"
        invite.PreLinkedPlayerId = 42;
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(invite);
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

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

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "test@example.com", null), default);

        result.IsSuccess.Should().BeTrue();

        unlinkedPlayer.AppUserId.Should().Be(appUserId);
        unlinkedPlayer.FirstName.Should().Be("John");
        unlinkedPlayer.LastName.Should().Be("Doe");
        unlinkedPlayer.Email.Should().Be("test@example.com");

        playerRepo.Verify(r => r.UpdateAsync(unlinkedPlayer, default), Times.Once);
        playerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Never);
        handicapRepo.Verify(r => r.AddAsync(It.IsAny<Handicap>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_AcceptInviteNoPreLink_GrantsMembershipOnly()
    {
        // No pre-linked player: user gets membership and role but no player profile.
        var inviteRepo = new Mock<IInviteRepository>();
        inviteRepo.Setup(r => r.GetByTokenAsync("token-123", default))
            .ReturnsAsync(MakeInvite());
        inviteRepo.Setup(r => r.UpdateAsync(It.IsAny<PlayerInvite>(), default)).Returns(Task.CompletedTask);

        var appUserId = Guid.NewGuid();
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.GetByAppUserIdAsync(appUserId, default))
            .ReturnsAsync((Player?)null);

        var leagueRepo = NoLeagueRepo();
        var roleService = NoExistingRoles();

        var handler = new AcceptInviteCommandHandler(
            inviteRepo.Object,
            playerRepo.Object,
            new Mock<IHandicapRepository>().Object,
            leagueRepo.Object,
            roleService.Object,
            new Mock<ILogger<AcceptInviteCommandHandler>>().Object);

        var result = await handler.Handle(new AcceptInviteCommand("token-123", appUserId, "John", "Doe", "test@example.com", null), default);

        result.IsSuccess.Should().BeTrue();
        playerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), default), Times.Never);
        playerRepo.Verify(r => r.UpdateAsync(It.IsAny<Player>(), default), Times.Never);
        leagueRepo.Verify(r => r.AddMembershipAsync(It.IsAny<LeagueMembership>(), default), Times.Once);
    }
}
