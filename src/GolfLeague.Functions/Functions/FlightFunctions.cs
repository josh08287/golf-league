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
        var result = await _mediator.Send(new GetFlightsQuery(), cancellationToken);
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
            new CreateFlightCommand(body.Name, body.SeasonId, body.DisplayOrder ?? 0, userId),
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

        if (!int.TryParse(req.Query["seasonId"], out var seasonId))
            return new BadRequestObjectResult(new { error = "Query parameter 'seasonId' is required." });

        var result = await _mediator.Send(new GetFlightStandingsQuery(flightId, seasonId), cancellationToken);
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

    private sealed record CreateFlightRequest(
        string Name,
        int? SeasonId,
        int? DisplayOrder);
}
