using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Seasons.Commands;
using GolfLeague.Application.Seasons.Queries;
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

public class SeasonFunctionsTests
{
    private static SeasonDto MakeSeasonDto(int id = 1) =>
        new(id, "2026 Season", 2026, "2026-04-01", "2026-10-31", true, null);

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
    public async Task GetSeasons_ReturnsOkResult()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<SeasonDto>>.Ok([MakeSeasonDto()]));

        var sut = new SeasonFunctions(mediator.Object);
        var result = await sut.GetSeasons(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateSeason_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "2026 Season", Year = 2026, StartDate = "2026-04-01", EndDate = "2026-10-31" });

        var result = await sut.CreateSeason(MakeRequest(body), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateSeason_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.CreateSeason(req, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateSeason_WhenInvalidDates_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "2026 Season", Year = 2026, StartDate = "not-a-date", EndDate = "2026-10-31" });

        var result = await sut.CreateSeason(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateSeason_WhenValid_ReturnsCreated()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateSeasonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SeasonDto>.Ok(MakeSeasonDto()));

        var sut = new SeasonFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "2026 Season", Year = 2026, StartDate = "2026-04-01", EndDate = "2026-10-31" });

        var result = await sut.CreateSeason(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task SetActiveSeason_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);

        var result = await sut.SetActiveSeason(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetActiveSeason_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);

        var result = await sut.SetActiveSeason(MakeRequest(role: "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetActiveSeason_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<SetActiveSeasonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SeasonDto>.Ok(MakeSeasonDto()));

        var sut = new SeasonFunctions(mediator.Object);

        var result = await sut.SetActiveSeason(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteSeason_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);

        var result = await sut.DeleteSeason(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteSeason_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new SeasonFunctions(mediator.Object);

        var result = await sut.DeleteSeason(MakeRequest(role: "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteSeason_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<DeleteSeasonCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Ok(true));

        var sut = new SeasonFunctions(mediator.Object);

        var result = await sut.DeleteSeason(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
