using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Auth;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

/// <summary>
/// A background token refresh that doesn't name a league must keep the
/// session's league. Otherwise a SuperAdmin's refreshed token carries no
/// league scope, and the query filters then return every league's data
/// (e.g. another league's live round on the leaderboard).
/// </summary>
public class RefreshTokenLeagueScopeTests : IDisposable
{
    private const int LeagueId = 3;

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<ILeagueRepository> _leagues = new();
    private readonly AppUser _superAdmin = new() { Id = Guid.NewGuid(), Email = "admin@example.com", UserName = "admin@example.com", IsSuperAdmin = true };

    public RefreshTokenLeagueScopeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        var leagueContext = new LeagueContext();
        leagueContext.Set(null, isSuperAdmin: true);
        _context = new AppDbContext(options, leagueContext);
        _context.Database.EnsureCreated();
        _context.Users.Add(_superAdmin);
        _context.SaveChanges();

        _tokens.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns<string>(s => s);
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("next-refresh");
        _tokens.Setup(t => t.IssueAccessToken(It.IsAny<AppUser>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool>()))
            .Returns(new AccessTokenResult("access", DateTime.UtcNow.AddMinutes(15)));
        _leagues.Setup(l => l.GetByIdAsync(LeagueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new League { Id = LeagueId, Name = "Mine", Slug = "mine" });
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private AuthService BuildSut()
    {
        var store = new Mock<IUserStore<AppUser>>();
        store.As<IUserRoleStore<AppUser>>()
            .Setup(s => s.GetRolesAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        var userManager = new UserManager<AppUser>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        return new AuthService(
            userManager,
            _tokens.Object,
            _context,
            new Mock<IPlayerRepository>().Object,
            new Mock<IInviteRepository>().Object,
            new Mock<IHandicapRepository>().Object,
            _leagues.Object,
            new Mock<IEmailService>().Object,
            new AuditWriter(new Mock<IAuditRepository>().Object, new Mock<ILogger<AuditWriter>>().Object),
            new Mock<ILogger<AuthService>>().Object);
    }

    private void StoreRefreshToken(string token, int? leagueId)
    {
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = _superAdmin.Id,
            TokenHash = token,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            LeagueId = leagueId,
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task Refresh_WithoutLeagueId_KeepsTheSessionsLeague()
    {
        StoreRefreshToken("old-refresh", LeagueId);

        var result = await BuildSut().RefreshAsync("old-refresh", leagueId: null);

        result.IsSuccess.Should().BeTrue();
        _tokens.Verify(t => t.IssueAccessToken(It.IsAny<AppUser>(), It.IsAny<IEnumerable<string>>(), LeagueId, It.IsAny<int?>(), true), Times.Once);
        _context.RefreshTokens.AsNoTracking().Single(r => r.TokenHash == "next-refresh").LeagueId
            .Should().Be(LeagueId, "the rotated refresh token must carry the league forward too");
    }

    [Fact]
    public async Task Refresh_WithExplicitLeagueId_SwitchesLeague()
    {
        StoreRefreshToken("old-refresh", leagueId: 99);

        var result = await BuildSut().RefreshAsync("old-refresh", leagueId: LeagueId);

        result.IsSuccess.Should().BeTrue();
        _tokens.Verify(t => t.IssueAccessToken(It.IsAny<AppUser>(), It.IsAny<IEnumerable<string>>(), LeagueId, It.IsAny<int?>(), true), Times.Once);
    }
}
