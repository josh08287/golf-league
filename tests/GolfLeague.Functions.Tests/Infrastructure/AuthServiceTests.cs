using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Auth;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

/// <summary>
/// Covers AuthService.ConsumeInviteAsync and ResolveLeagueAsync — the
/// invite-consumption and league-resolution logic underpinning the
/// multi-league Player change. Both are internal (ResolveLeagueAsync was
/// changed from private to internal solely to enable this) and touch only
/// repository interfaces, so they're testable with Moq alone;
/// AppDbContext/UserManager are constructed but never invoked by either.
/// </summary>
public class AuthServiceTests
{
    private static UserManager<AppUser> MakeUserManager()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new UserManager<AppUser>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static AppDbContext MakeUnusedDbContext()
    {
        // Never queried by ConsumeInviteAsync/ResolveLeagueAsync — a
        // SqlServer-backed context is fine since EF Core doesn't open a
        // connection until a query actually runs.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;")
            .Options;
        return new AppDbContext(options);
    }

    private sealed class Mocks
    {
        public Mock<IPlayerRepository> PlayerRepo { get; } = new();
        public Mock<IInviteRepository> InviteRepo { get; } = new();
        public Mock<IHandicapRepository> HandicapRepo { get; } = new();
        public Mock<ILeagueRepository> LeagueRepo { get; } = new();
        public Mock<IEmailService> EmailService { get; } = new();
        public Mock<ITokenService> TokenService { get; } = new();
        public Mock<IAuditRepository> AuditRepo { get; } = new();

        public AuthService BuildSut() => new(
            MakeUserManager(),
            TokenService.Object,
            MakeUnusedDbContext(),
            PlayerRepo.Object,
            InviteRepo.Object,
            HandicapRepo.Object,
            LeagueRepo.Object,
            EmailService.Object,
            AuditRepo.Object,
            new Mock<ILogger<AuthService>>().Object);
    }

    private static PlayerInvite MakeInvite(int leagueId = 1, PlayerRole role = PlayerRole.Player, int? preLinkedPlayerId = null) => new()
    {
        Id = 1,
        LeagueId = leagueId,
        Email = "invitee@example.com",
        Token = "tok",
        Status = InviteStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        Role = role,
        PreLinkedPlayerId = preLinkedPlayerId,
    };

    private static AppUser MakeUser() => new() { Id = Guid.NewGuid(), Email = "invitee@example.com" };

    // ── ConsumeInviteAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ConsumeInviteAsync_WhenUserAlreadyHasPlayerInThisLeague_ReusesExistingPlayerIdempotently()
    {
        var m = new Mocks();
        var user = MakeUser();
        var invite = MakeInvite(leagueId: 5);
        var existingPlayer = new Player { Id = 42, LeagueId = 5, AppUserId = user.Id, FirstName = "Jane", LastName = "Roe" };
        m.PlayerRepo.Setup(r => r.GetByAppUserIdAsync(user.Id, 5, It.IsAny<CancellationToken>())).ReturnsAsync(existingPlayer);

        var sut = m.BuildSut();
        await sut.ConsumeInviteAsync(invite, user, "First", "Last", CancellationToken.None);

        invite.PlayerId.Should().Be(42);
        invite.Status.Should().Be(InviteStatus.Accepted);
        invite.AcceptedByAppUserId.Should().Be(user.Id);
        // Idempotent re-accept: no new Player row created.
        m.PlayerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()), Times.Never);
        m.PlayerRepo.Verify(r => r.UpdateAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenPreLinkedPlayerExists_AdoptsItAndCopiesInName()
    {
        var m = new Mocks();
        var user = MakeUser();
        var invite = MakeInvite(leagueId: 5, preLinkedPlayerId: 7);
        m.PlayerRepo.Setup(r => r.GetByAppUserIdAsync(user.Id, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);
        var preLink = new Player { Id = 7, LeagueId = 5, AppUserId = null, FirstName = "Old", LastName = "Name" };
        m.PlayerRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(preLink);

        var sut = m.BuildSut();
        await sut.ConsumeInviteAsync(invite, user, "New", "Name", CancellationToken.None);

        preLink.AppUserId.Should().Be(user.Id);
        preLink.FirstName.Should().Be("New");
        preLink.LastName.Should().Be("Name");
        invite.PlayerId.Should().Be(7);
        invite.Status.Should().Be(InviteStatus.Accepted);
        m.PlayerRepo.Verify(r => r.UpdateAsync(preLink, It.IsAny<CancellationToken>()), Times.Once);
        m.LeagueRepo.Verify(r => r.AddMembershipAsync(
            It.Is<LeagueMembership>(lm => lm.LeagueId == 5 && lm.UserId == user.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenPreLinkedPlayerAlreadyLinkedElsewhere_FallsThroughToNewPlayer()
    {
        // The pre-linked player got claimed by someone else between invite
        // creation and acceptance — must not steal it; fall through instead.
        var m = new Mocks();
        var user = MakeUser();
        var invite = MakeInvite(leagueId: 5, preLinkedPlayerId: 7);
        m.PlayerRepo.Setup(r => r.GetByAppUserIdAsync(user.Id, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);
        var preLink = new Player { Id = 7, LeagueId = 5, AppUserId = Guid.NewGuid(), FirstName = "Someone", LastName = "Else" };
        m.PlayerRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(preLink);

        var sut = m.BuildSut();
        await sut.ConsumeInviteAsync(invite, user, "First", "Last", CancellationToken.None);

        // No pre-linked player and no existing player in this league — the
        // "no pre-linked player" branch creates only a LeagueMembership, no
        // Player row (per ConsumeInviteAsync's documented "membership only"
        // behavior when nothing is pre-linked).
        invite.Status.Should().Be(InviteStatus.Accepted);
        invite.PlayerId.Should().BeNull();
        preLink.AppUserId.Should().NotBe(user.Id);
        m.PlayerRepo.Verify(r => r.UpdateAsync(preLink, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenNoExistingPlayerAndNoPreLink_GrantsMembershipOnlyWithNoPlayerRow()
    {
        var m = new Mocks();
        var user = MakeUser();
        var invite = MakeInvite(leagueId: 5);
        m.PlayerRepo.Setup(r => r.GetByAppUserIdAsync(user.Id, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);

        var sut = m.BuildSut();
        await sut.ConsumeInviteAsync(invite, user, "First", "Last", CancellationToken.None);

        invite.Status.Should().Be(InviteStatus.Accepted);
        invite.AcceptedByAppUserId.Should().Be(user.Id);
        invite.PlayerId.Should().BeNull();
        m.PlayerRepo.Verify(r => r.AddAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()), Times.Never);
        m.LeagueRepo.Verify(r => r.AddMembershipAsync(
            It.Is<LeagueMembership>(lm => lm.LeagueId == 5 && lm.UserId == user.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        m.InviteRepo.Verify(r => r.UpdateAsync(invite, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeInviteAsync_WhenUserHasPlayerInAnotherLeagueOnly_DoesNotReuseIt()
    {
        // Regression guard for the multi-league fix: GetByAppUserIdAsync is
        // scoped to invite.LeagueId, so a Player in a *different* league must
        // never be returned/reused here.
        var m = new Mocks();
        var user = MakeUser();
        var invite = MakeInvite(leagueId: 5);
        m.PlayerRepo.Setup(r => r.GetByAppUserIdAsync(user.Id, 5, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);

        var sut = m.BuildSut();
        await sut.ConsumeInviteAsync(invite, user, "First", "Last", CancellationToken.None);

        m.PlayerRepo.Verify(r => r.GetByAppUserIdAsync(user.Id, 5, It.IsAny<CancellationToken>()), Times.Once);
        m.PlayerRepo.Verify(r => r.GetByAppUserIdAsync(user.Id, It.Is<int>(l => l != 5), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── ResolveLeagueAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ResolveLeagueAsync_SuperAdminWithValidLeagueId_ReturnsAdminRole()
    {
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = true };
        m.LeagueRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new League { Id = 3, Name = "L", Slug = "l" });

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, 3, CancellationToken.None);

        leagueId.Should().Be(3);
        role.Should().Be("admin");
    }

    [Fact]
    public async Task ResolveLeagueAsync_SuperAdminWithNonexistentLeagueId_ReturnsNull()
    {
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = true };
        m.LeagueRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((League?)null);

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, 99, CancellationToken.None);

        leagueId.Should().BeNull();
        role.Should().BeNull();
    }

    [Fact]
    public async Task ResolveLeagueAsync_SuperAdminWithoutLeagueId_ReturnsNull()
    {
        // SuperAdmin gets no auto-selected league — must explicitly choose one.
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = true };

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, null, CancellationToken.None);

        leagueId.Should().BeNull();
        role.Should().BeNull();
        m.LeagueRepo.Verify(r => r.GetMembershipsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveLeagueAsync_RegularUserNoLeagueIdWithExactlyOneMembership_AutoSelects()
    {
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = false };
        m.LeagueRepo.Setup(r => r.GetMembershipsForUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeagueMembership> { new() { LeagueId = 8, Role = PlayerRole.Scorer } });

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, null, CancellationToken.None);

        leagueId.Should().Be(8);
        role.Should().Be("scorer");
    }

    [Fact]
    public async Task ResolveLeagueAsync_RegularUserNoLeagueIdWithMultipleMemberships_ReturnsNull()
    {
        // Ambiguous — caller must specify which league explicitly.
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = false };
        m.LeagueRepo.Setup(r => r.GetMembershipsForUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeagueMembership>
            {
                new() { LeagueId = 1, Role = PlayerRole.Player },
                new() { LeagueId = 2, Role = PlayerRole.Player },
            });

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, null, CancellationToken.None);

        leagueId.Should().BeNull();
        role.Should().BeNull();
    }

    [Fact]
    public async Task ResolveLeagueAsync_RegularUserNoLeagueIdWithZeroMemberships_ReturnsNull()
    {
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = false };
        m.LeagueRepo.Setup(r => r.GetMembershipsForUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeagueMembership>());

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, null, CancellationToken.None);

        leagueId.Should().BeNull();
        role.Should().BeNull();
    }

    [Fact]
    public async Task ResolveLeagueAsync_RegularUserWithExplicitLeagueIdAndMembership_ReturnsMembershipRole()
    {
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = false };
        m.LeagueRepo.Setup(r => r.GetMembershipAsync(4, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeagueMembership { LeagueId = 4, Role = PlayerRole.Admin });

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, 4, CancellationToken.None);

        leagueId.Should().Be(4);
        role.Should().Be("admin");
    }

    [Fact]
    public async Task ResolveLeagueAsync_RegularUserWithExplicitLeagueIdButNoMembership_ReturnsNull()
    {
        // Requesting a league the user doesn't belong to must not grant access.
        var m = new Mocks();
        var user = new AppUser { Id = Guid.NewGuid(), IsSuperAdmin = false };
        m.LeagueRepo.Setup(r => r.GetMembershipAsync(4, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeagueMembership?)null);

        var sut = m.BuildSut();
        var (leagueId, role) = await sut.ResolveLeagueAsync(user, 4, CancellationToken.None);

        leagueId.Should().BeNull();
        role.Should().BeNull();
    }
}
