using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
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

public class RoundFunctionsTests
{
    private static RoundDto MakeRoundDto(int id = 1) =>
        new(id, 1, 1, "A Flight", 1, "Course A", DateOnly.FromDateTime(DateTime.UtcNow), RoundStatus.Scheduled, 0);

    private static HttpRequest MakeRequest(string? body = null, string? role = null, string? query = null)
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
        if (query is not null)
            context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [Fact]
    public async Task GetRounds_ReturnsOkResult()
    {
        var mediator = new Mock<IMediator>();
        var paged = new PagedResult<RoundDto>([MakeRoundDto()], 1, 20, 1);
        mediator.Setup(m => m.Send(It.IsAny<GetRoundsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<RoundDto>>.Ok(paged));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRounds(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRounds_WithSeasonId_PassesSeasonIdToQuery()
    {
        var mediator = new Mock<IMediator>();
        var paged = new PagedResult<RoundDto>([], 1, 20, 0);
        GetRoundsQuery? capturedQuery = null;
        mediator.Setup(m => m.Send(It.IsAny<GetRoundsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<PagedResult<RoundDto>>>, CancellationToken>((q, _) => capturedQuery = (GetRoundsQuery)q)
            .ReturnsAsync(Result<PagedResult<RoundDto>>.Ok(paged));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        await sut.GetRounds(MakeRequest(query: "?seasonId=5"), CancellationToken.None);

        capturedQuery!.SeasonId.Should().Be(5);
    }

    [Fact]
    public async Task GetRound_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRound(MakeRequest(), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRound_WhenNotFound_ReturnsNotFound()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRoundQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Fail("Round with ID 99 not found."));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRound(MakeRequest(), "99", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetRound_WhenFound_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRoundQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Ok(MakeRoundDto()));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRound(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateRound_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { FlightId = 1, CourseId = 1, SeasonId = 1, RoundDate = "2026-05-01" });

        var result = await sut.CreateRound(MakeRequest(body), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateRound_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.CreateRound(req, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateRound_WhenNoSeasonAndNoActiveSeason_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetActiveSeasonIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { FlightId = 1, CourseId = 1, RoundDate = "2026-05-01" });

        var result = await sut.CreateRound(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateRound_WhenNoSeasonIdButActiveExists_UsesActiveSeason()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateRoundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Ok(MakeRoundDto()));

        var flightRepo = new Mock<IFlightRepository>();
        flightRepo.Setup(r => r.GetActiveSeasonIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { FlightId = 1, CourseId = 1, RoundDate = "2026-05-01" });

        var result = await sut.CreateRound(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task CreateRound_WhenValid_ReturnsCreated()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateRoundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Ok(MakeRoundDto()));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { SeasonId = 1, FlightId = 1, CourseId = 1, RoundDate = "2026-05-01" });

        var result = await sut.CreateRound(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task CreateRound_WithScheduledDate_ReturnsCreated()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateRoundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Ok(MakeRoundDto()));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { SeasonId = 1, FlightId = 1, CourseId = 1, ScheduledDate = "2026-05-01" });

        var result = await sut.CreateRound(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task CreateRound_WithNeitherDate_UsesUtcNow()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateRoundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Ok(MakeRoundDto()));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { SeasonId = 1, FlightId = 1, CourseId = 1 });

        var result = await sut.CreateRound(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task GetPlayerScorecard_WhenInvalidIds_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetPlayerScorecard(MakeRequest(), "abc", "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPlayerScorecard_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var scorecardDto = new ScorecardDto(1, DateOnly.FromDateTime(DateTime.UtcNow), "Course A", 72.0, 113,
            new ParticipantDto(1, 1, 1, "John Doe", "JD", 10.0, 10, null, null, null, false),
            [], 36, 36, 72, 36, 36, 34, 34, 10, 10);

        mediator.Setup(m => m.Send(It.IsAny<GetPlayerScorecardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ScorecardDto>.Ok(scorecardDto));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetPlayerScorecard(MakeRequest(), "1", "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPlayerScorecardByRoute_WhenInvalidIds_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetPlayerScorecardByRoute(MakeRequest(), "1", "xyz", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPlayerScorecardByRoute_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var scorecardDto = new ScorecardDto(1, DateOnly.FromDateTime(DateTime.UtcNow), "Course A", 72.0, 113,
            new ParticipantDto(1, 1, 1, "John Doe", "JD", 10.0, 10, null, null, null, false),
            [], 36, 36, 72, 36, 36, 34, 34, 10, 10);

        mediator.Setup(m => m.Send(It.IsAny<GetPlayerScorecardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ScorecardDto>.Ok(scorecardDto));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetPlayerScorecardByRoute(MakeRequest(), "1", "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRoundScorecards_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRoundScorecards(MakeRequest(), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRoundScorecards_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var paged = new PagedResult<RoundScorecardDto>([], 1, 0, 0);
        mediator.Setup(m => m.Send(It.IsAny<GetRoundScorecardsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<RoundScorecardDto>>.Ok(paged));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRoundScorecards(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRoundParticipants_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRoundParticipants(MakeRequest(), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetRoundParticipants_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetRoundParticipantsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<RoundParticipantDto>>.Ok([]));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.GetRoundParticipants(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmitHoleScores_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { HoleScores = new[] { new { HoleNumber = 1, GrossStrokes = 4 } } });

        var result = await sut.SubmitHoleScores(MakeRequest(body), "1", "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SubmitHoleScores_WhenInvalidIds_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { HoleScores = new[] { new { HoleNumber = 1, GrossStrokes = 4 } } });

        var result = await sut.SubmitHoleScores(MakeRequest(body, "scorer"), "abc", "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitHoleScores_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var req = MakeRequest(role: "scorer");
        req.Body = new MemoryStream();

        var result = await sut.SubmitHoleScores(req, "1", "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SubmitHoleScores_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var scorecardDto = new ScorecardDto(1, DateOnly.FromDateTime(DateTime.UtcNow), "Course A", 72.0, 113,
            new ParticipantDto(1, 1, 1, "John Doe", "JD", 10.0, 10, null, null, null, false),
            [], 36, 36, 72, 36, 36, 34, 34, 10, 10);

        mediator.Setup(m => m.Send(It.IsAny<SubmitHoleScoresCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ScorecardDto>.Ok(scorecardDto));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        var body = JsonSerializer.Serialize(new { HoleScores = new[] { new { HoleNumber = 1, GrossStrokes = 4 } } });

        var result = await sut.SubmitHoleScores(MakeRequest(body, "scorer"), "1", "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SubmitHoleScores_WithScoresAlias_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var scorecardDto = new ScorecardDto(1, DateOnly.FromDateTime(DateTime.UtcNow), "Course A", 72.0, 113,
            new ParticipantDto(1, 1, 1, "John Doe", "JD", 10.0, 10, null, null, null, false),
            [], 36, 36, 72, 36, 36, 34, 34, 10, 10);

        mediator.Setup(m => m.Send(It.IsAny<SubmitHoleScoresCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ScorecardDto>.Ok(scorecardDto));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);
        // Use "Scores" alias instead of "HoleScores", and "GrossScore" instead of "GrossStrokes"
        var body = JsonSerializer.Serialize(new { Scores = new[] { new { HoleNumber = 1, GrossScore = 4 } } });

        var result = await sut.SubmitHoleScores(MakeRequest(body, "scorer"), "1", "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task FinalizeRound_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.FinalizeRound(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task FinalizeRound_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.FinalizeRound(MakeRequest(role: "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task FinalizeRound_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<FinalizeRoundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RoundDto>.Ok(MakeRoundDto()));

        var flightRepo = new Mock<IFlightRepository>();
        var sut = new RoundFunctions(mediator.Object, flightRepo.Object);

        var result = await sut.FinalizeRound(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
