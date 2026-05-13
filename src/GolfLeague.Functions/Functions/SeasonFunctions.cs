using GolfLeague.Application.Common;
using GolfLeague.Application.Seasons.Commands;
using GolfLeague.Application.Seasons.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Globalization;

namespace GolfLeague.Functions.Functions;

public sealed class SeasonFunctions
{
    private readonly IMediator _mediator;

    public SeasonFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetSeasons")]
    public async Task<IActionResult> GetSeasons(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/seasons")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);
        var result = await _mediator.Send(new GetSeasonsQuery(sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreateSeason")]
    public async Task<IActionResult> CreateSeason(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seasons")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateSeasonRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        if (!DateOnly.TryParseExact(body.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) ||
            !DateOnly.TryParseExact(body.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            return new BadRequestObjectResult(new { error = "startDate and endDate must be valid dates (yyyy-MM-dd)." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new CreateSeasonCommand(body.Name, body.Year, startDate, endDate, body.BestNRounds, userId),
            cancellationToken);

        return result.ToCreatedResult($"/api/v1/seasons/{result.Value?.Id}");
    }

    [Function("SetActiveSeason")]
    public async Task<IActionResult> SetActiveSeason(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/seasons/{id}/activate")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var seasonId))
            return new BadRequestObjectResult(new { error = "Invalid season ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetActiveSeasonCommand(seasonId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("DeleteSeason")]
    public async Task<IActionResult> DeleteSeason(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/seasons/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var seasonId))
            return new BadRequestObjectResult(new { error = "Invalid season ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeleteSeasonCommand(seasonId, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreateSeasonRequest(
        string Name,
        int Year,
        string StartDate,
        string EndDate,
        int? BestNRounds);
}
