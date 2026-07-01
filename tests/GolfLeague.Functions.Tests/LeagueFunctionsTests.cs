using FluentAssertions;
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
/// LeagueFunctions bypasses MediatR for league CRUD (GetLeagues,
/// GetLeagueBySlug, CreateLeague, UpdateLeague), hand-rolling slug
/// validation, duplicate checks, and SuperAdmin branching directly in the
/// Function. None of this had prior test coverage.
/// </summary>
public class LeagueFunctionsTests
{
    private static HttpRequest MakeRequest(string? body = null, bool superAdmin = false, bool authenticated = false)
    {
        var context = new DefaultHttpContext();
        if (superAdmin || authenticated)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user1") };
            if (superAdmin) claims.Add(new Claim("superAdmin", "true"));
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

    private static League MakeLeague(int id = 1, string name = "Riverside", string slug = "riverside", bool isActive = true) =>
        new() { Id = id, Name = name, Slug = slug, IsActive = isActive, CreatedAt = DateTime.UtcNow };

    private static LeagueFunctions MakeSut(Mock<ILeagueRepository>? repo = null, Mock<IMediator>? mediator = null) =>
        new((repo ?? new Mock<ILeagueRepository>()).Object, (mediator ?? new Mock<IMediator>()).Object);

    // ── GetLeagues ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetLeagues_WhenAnonymous_ReturnsOnlyActiveLeagues()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<League> { MakeLeague(1, isActive: true), MakeLeague(2, isActive: false) });
        var sut = MakeSut(repo);

        var result = await sut.GetLeagues(MakeRequest(), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"Id\":1");
        json.Should().NotContain("\"Id\":2");
    }

    [Fact]
    public async Task GetLeagues_WhenSuperAdmin_ReturnsInactiveLeaguesToo()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<League> { MakeLeague(1, isActive: true), MakeLeague(2, isActive: false) });
        var sut = MakeSut(repo);

        var result = await sut.GetLeagues(MakeRequest(superAdmin: true), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("\"Id\":1");
        json.Should().Contain("\"Id\":2");
    }

    // ── GetLeagueBySlug ──────────────────────────────────────────────────

    [Fact]
    public async Task GetLeagueBySlug_WhenNotFound_ReturnsNotFound()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetBySlugAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((League?)null);
        var sut = MakeSut(repo);

        var result = await sut.GetLeagueBySlug(MakeRequest(), "missing", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetLeagueBySlug_WhenFound_ReturnsOk()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetBySlugAsync("riverside", It.IsAny<CancellationToken>())).ReturnsAsync(MakeLeague());
        var sut = MakeSut(repo);

        var result = await sut.GetLeagueBySlug(MakeRequest(), "riverside", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── CreateLeague ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateLeague_WhenNotSuperAdmin_ReturnsForbidden()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { Name = "New League", Slug = "new-league" });

        var result = await sut.CreateLeague(MakeRequest(body, authenticated: true), CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateLeague_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { Name = "New League", Slug = "new-league" });

        var result = await sut.CreateLeague(MakeRequest(body), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateLeague_WhenNameMissing_ReturnsBadRequest()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { Name = "", Slug = "new-league" });

        var result = await sut.CreateLeague(MakeRequest(body, superAdmin: true), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateLeague_WhenSlugMissing_ReturnsBadRequest()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { Name = "New League", Slug = "" });

        var result = await sut.CreateLeague(MakeRequest(body, superAdmin: true), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("Has Spaces")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    public async Task CreateLeague_WhenSlugHasInvalidCharacters_ReturnsBadRequest(string invalidSlug)
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { Name = "New League", Slug = invalidSlug });

        var result = await sut.CreateLeague(MakeRequest(body, superAdmin: true), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateLeague_NormalizesSlugToLowercaseAndTrims()
    {
        League? captured = null;
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetBySlugAsync("new-league", It.IsAny<CancellationToken>())).ReturnsAsync((League?)null);
        repo.Setup(r => r.AddAsync(It.IsAny<League>(), It.IsAny<CancellationToken>()))
            .Callback<League, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { Name = "  New League  ", Slug = "  NEW-LEAGUE  " });

        await sut.CreateLeague(MakeRequest(body, superAdmin: true), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Slug.Should().Be("new-league");
        captured.Name.Should().Be("New League");
    }

    [Fact]
    public async Task CreateLeague_WhenSlugAlreadyExists_ReturnsConflict()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetBySlugAsync("riverside", It.IsAny<CancellationToken>())).ReturnsAsync(MakeLeague());
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { Name = "Riverside Golf", Slug = "riverside" });

        var result = await sut.CreateLeague(MakeRequest(body, superAdmin: true), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
        repo.Verify(r => r.AddAsync(It.IsAny<League>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateLeague_WhenValid_ReturnsCreatedWithNewLeagueActiveByDefault()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetBySlugAsync("new-league", It.IsAny<CancellationToken>())).ReturnsAsync((League?)null);
        League? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<League>(), It.IsAny<CancellationToken>()))
            .Callback<League, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { Name = "New League", Slug = "new-league" });

        var result = await sut.CreateLeague(MakeRequest(body, superAdmin: true), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        captured!.IsActive.Should().BeTrue();
        repo.Verify(r => r.AddAsync(It.IsAny<League>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateLeague ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateLeague_WhenNotSuperAdmin_ReturnsForbidden()
    {
        var sut = MakeSut();
        var body = JsonSerializer.Serialize(new { Name = "Renamed" });

        var result = await sut.UpdateLeague(MakeRequest(body, authenticated: true), 1, CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateLeague_WhenLeagueNotFound_ReturnsNotFound()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((League?)null);
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { Name = "Renamed" });

        var result = await sut.UpdateLeague(MakeRequest(body, superAdmin: true), 99, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateLeague_WhenNoBody_ReturnsBadRequest()
    {
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(MakeLeague());
        var sut = MakeSut(repo);
        var req = MakeRequest(superAdmin: true);
        req.Body = new MemoryStream();

        var result = await sut.UpdateLeague(req, 1, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateLeague_WhenNameProvided_UpdatesNameOnly()
    {
        var league = MakeLeague(isActive: true);
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(league);
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { Name = "  Renamed League  " });

        var result = await sut.UpdateLeague(MakeRequest(body, superAdmin: true), 1, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        league.Name.Should().Be("Renamed League");
        league.IsActive.Should().BeTrue();
        repo.Verify(r => r.UpdateAsync(league, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateLeague_WhenIsActiveProvided_UpdatesActiveOnly()
    {
        var league = MakeLeague(name: "Riverside", isActive: true);
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(league);
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { IsActive = false });

        await sut.UpdateLeague(MakeRequest(body, superAdmin: true), 1, CancellationToken.None);

        league.IsActive.Should().BeFalse();
        league.Name.Should().Be("Riverside");
    }

    [Fact]
    public async Task UpdateLeague_WhenNameBlank_LeavesExistingNameUnchanged()
    {
        var league = MakeLeague(name: "Riverside");
        var repo = new Mock<ILeagueRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(league);
        var sut = MakeSut(repo);
        var body = JsonSerializer.Serialize(new { Name = "   " });

        await sut.UpdateLeague(MakeRequest(body, superAdmin: true), 1, CancellationToken.None);

        league.Name.Should().Be("Riverside");
    }
}
