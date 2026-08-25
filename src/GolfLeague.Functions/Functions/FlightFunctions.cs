using GolfLeague.Application.Common;
using GolfLeague.Application.Flights.Commands;
using GolfLeague.Application.Flights.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class FlightFunctions
{
    private readonly IMediator _mediator;

    public FlightFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetFlights")]
    public async Task<IActionResult> GetFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        int? halfId = int.TryParse(req.Query["halfId"], out var hid) ? hid : null;
        int? seasonId = int.TryParse(req.Query["seasonId"], out var sid) ? sid : null;
        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);
        var result = await _mediator.Send(new GetFlightsQuery(halfId, seasonId, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetFlight")]
    public async Task<IActionResult> GetFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var flightId))
            return new BadRequestObjectResult(new { error = "Invalid flight ID." });

        var result = await _mediator.Send(new GetFlightQuery(flightId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreateFlight")]
    public async Task<IActionResult> CreateFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateFlightRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new CreateFlightCommand(body.Name, body.HalfId, body.DisplayOrder ?? 0, userId),
            cancellationToken);
        return result.ToCreatedResult($"/api/v1/flights/{result.Value?.Id}");
    }

    [Function("GetFlightStandings")]
    public async Task<IActionResult> GetFlightStandings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{id}/standings")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var flightId))
            return new BadRequestObjectResult(new { error = "Invalid flight ID." });

        if (!int.TryParse(req.Query["halfId"], out var halfId))
            return new BadRequestObjectResult(new { error = "Query parameter 'halfId' is required." });

        var useGross = bool.TryParse(req.Query["useGrossPoints"], out var ug) && ug;
        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);

        var result = await _mediator.Send(new GetFlightStandingsQuery(flightId, halfId, useGross, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("DeleteFlight")]
    public async Task<IActionResult> DeleteFlight(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/flights/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var flightId))
            return new BadRequestObjectResult(new { error = "Invalid flight ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeleteFlightCommand(flightId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("InitializeHalfFlights")]
    public async Task<IActionResult> InitializeHalfFlights(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights/initialize-half")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<InitializeHalfRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new InitializeHalfFlightsCommand(body.HalfId, userId, body.MaxPlayersPerFlight ?? 8),
            cancellationToken);
        return result.ToOkResult();
    }

    [Function("GenerateMatchPlaySchedule")]
    public async Task<IActionResult> GenerateMatchPlaySchedule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flights/generate-match-schedule")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<GenerateMatchPlayScheduleRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new GenerateMatchPlayScheduleCommand(body.HalfId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetMatchPlayStandings")]
    public async Task<IActionResult> GetMatchPlayStandings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{id}/match-play-standings")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var flightId))
            return new BadRequestObjectResult(new { error = "Invalid flight ID." });

        if (!int.TryParse(req.Query["halfId"], out var halfId))
            return new BadRequestObjectResult(new { error = "Query parameter 'halfId' is required." });

        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);
        var result = await _mediator.Send(new GetMatchPlayStandingsQuery(flightId, halfId, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetFlightMatches")]
    public async Task<IActionResult> GetFlightMatches(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/matches")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(req.Query["halfId"], out var halfId))
            return new BadRequestObjectResult(new { error = "Query parameter 'halfId' is required." });

        int? flightId = int.TryParse(req.Query["flightId"], out var fid) ? fid : null;

        var result = await _mediator.Send(new GetFlightMatchesQuery(halfId, flightId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreateFlightRequest(
        string Name,
        int HalfId,
        int? DisplayOrder);

    private sealed record InitializeHalfRequest(
        int HalfId,
        int? MaxPlayersPerFlight);

    private sealed record GenerateMatchPlayScheduleRequest(int HalfId);
}
