using FluentAssertions;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Flights.Commands;
using GolfLeague.Application.Flights.Queries;
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

public class FlightFunctionsTests
{
    private static FlightDto MakeFlightDto(int id = 1) =>
        new(id, 1, null, "A Flight", 0, 0);

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
    public async Task GetFlights_ReturnsOkResult()
    {
        var mediator = new Mock<IMediator>();
        var pagedResult = new PagedResult<FlightDto>([MakeFlightDto()], 1, 20, 1);
        mediator.Setup(m => m.Send(It.IsAny<GetFlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PagedResult<FlightDto>>.Ok(pagedResult));

        var sut = new FlightFunctions(mediator.Object);
        var result = await sut.GetFlights(MakeRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateFlight_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new FlightFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "A Flight", DisplayOrder = 1 });

        var result = await sut.CreateFlight(MakeRequest(body), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateFlight_WhenNoBody_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new FlightFunctions(mediator.Object);
        var req = MakeRequest(role: "admin");
        req.Body = new MemoryStream();

        var result = await sut.CreateFlight(req, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateFlight_WhenAdminAndValid_ReturnsCreated()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateFlightCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FlightDto>.Ok(MakeFlightDto()));

        var sut = new FlightFunctions(mediator.Object);
        var body = JsonSerializer.Serialize(new { Name = "A Flight", DisplayOrder = 1 });
        var result = await sut.CreateFlight(MakeRequest(body, "admin"), CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task GetFlightStandings_WhenInvalidFlightId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new FlightFunctions(mediator.Object);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?seasonId=1");

        var result = await sut.GetFlightStandings(context.Request, "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFlightStandings_WhenNoSeasonId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new FlightFunctions(mediator.Object);
        var context = new DefaultHttpContext();

        var result = await sut.GetFlightStandings(context.Request, "1", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetFlightStandings_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetFlightStandingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<StandingDto>>.Ok([]));

        var sut = new FlightFunctions(mediator.Object);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?seasonId=1");

        var result = await sut.GetFlightStandings(context.Request, "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteFlight_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var mediator = new Mock<IMediator>();
        var sut = new FlightFunctions(mediator.Object);

        var result = await sut.DeleteFlight(MakeRequest(), "1", CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteFlight_WhenInvalidId_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var sut = new FlightFunctions(mediator.Object);

        var result = await sut.DeleteFlight(MakeRequest(role: "admin"), "abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteFlight_WhenValid_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<DeleteFlightCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Ok(true));

        var sut = new FlightFunctions(mediator.Object);

        var result = await sut.DeleteFlight(MakeRequest(role: "admin"), "1", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
