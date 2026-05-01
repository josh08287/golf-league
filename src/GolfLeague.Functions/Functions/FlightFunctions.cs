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

    [Function("GetFlightStandings")]
    public async Task<IActionResult> GetFlightStandings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flights/{id:int}/standings")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(req.Query["seasonId"], out var seasonId))
            return new BadRequestObjectResult(new { error = "Query parameter 'seasonId' is required." });

        var result = await _mediator.Send(new GetFlightStandingsQuery(id, seasonId), cancellationToken);
        return result.ToOkResult();
    }
}
