using GolfLeague.Application.Statistics.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class StatisticsFunctions
{
    private readonly IMediator _mediator;

    public StatisticsFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetCourseStatistics")]
    public async Task<IActionResult> GetCourseStatistics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/courses/{id}/statistics")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var courseId))
            return new BadRequestObjectResult(new { error = "Invalid course ID." });

        var result = await _mediator.Send(new GetCourseStatisticsQuery(courseId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetPlayerStatistics")]
    public async Task<IActionResult> GetPlayerStatistics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/players/{id}/statistics")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var playerId))
            return new BadRequestObjectResult(new { error = "Invalid player ID." });

        var result = await _mediator.Send(new GetPlayerStatisticsQuery(playerId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetMostImprovedPlayer")]
    public async Task<IActionResult> GetMostImprovedPlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/statistics/most-improved")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMostImprovedPlayerQuery(), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetLeagueLeaderboards")]
    public async Task<IActionResult> GetLeagueLeaderboards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/statistics/leaderboards")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeagueLeaderboardsQuery(), cancellationToken);
        return result.ToOkResult();
    }
}
