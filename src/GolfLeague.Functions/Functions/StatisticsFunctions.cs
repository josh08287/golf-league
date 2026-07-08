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

        var (seasonId, halfId, _) = ParsePeriod(req);
        var result = await _mediator.Send(new GetCourseStatisticsQuery(courseId, seasonId, halfId), cancellationToken);
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
        var (seasonId, halfId, allTime) = ParsePeriod(req);
        var result = await _mediator.Send(new GetMostImprovedPlayerQuery(seasonId, halfId, allTime), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetLeagueLeaderboards")]
    public async Task<IActionResult> GetLeagueLeaderboards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/statistics/leaderboards")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var (seasonId, halfId, _) = ParsePeriod(req);
        var result = await _mediator.Send(new GetLeagueLeaderboardsQuery(seasonId, halfId), cancellationToken);
        return result.ToOkResult();
    }

    private static (int? SeasonId, int? HalfId, bool AllTime) ParsePeriod(HttpRequest req)
    {
        int? seasonId = int.TryParse(req.Query["seasonId"], out var s) ? s : null;
        int? halfId = int.TryParse(req.Query["halfId"], out var h) ? h : null;
        bool allTime = string.Equals(req.Query["all"], "true", StringComparison.OrdinalIgnoreCase);
        return (seasonId, halfId, allTime);
    }
}
