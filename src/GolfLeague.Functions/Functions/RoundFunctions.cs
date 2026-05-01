using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;

namespace GolfLeague.Functions.Functions;

public sealed class RoundFunctions
{
    private readonly IMediator _mediator;

    public RoundFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetRounds")]
    public async Task<IActionResult> GetRounds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(req.Query["seasonId"], out var seasonId))
            return new BadRequestObjectResult(new { error = "Query parameter 'seasonId' is required." });

        var page = int.TryParse(req.Query["page"], out var p) ? p : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 20;

        var result = await _mediator.Send(new GetRoundsQuery(seasonId, page, pageSize), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetRound")]
    public async Task<IActionResult> GetRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id:int}")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRoundQuery(id), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreateRound")]
    public async Task<IActionResult> CreateRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await JsonSerializer.DeserializeAsync<CreateRoundRequest>(
            req.Body,
            JsonSerializerOptions.Web,
            cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var participants = body.PlayerIds
            .Select(pid => new CreateRoundParticipantInput(pid))
            .ToList();

        var command = new CreateRoundCommand(
            body.SeasonId,
            body.FlightId,
            body.CourseId,
            body.RoundDate,
            body.Notes,
            participants,
            userId);

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToCreatedResult($"/api/v1/rounds/{result.Value?.Id}");
    }

    [Function("GetPlayerScorecard")]
    public async Task<IActionResult> GetPlayerScorecard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id:int}/scores/{playerId:int}")] HttpRequest req,
        int id,
        int playerId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPlayerScorecardQuery(id, playerId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SubmitHoleScores")]
    public async Task<IActionResult> SubmitHoleScores(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/rounds/{id:int}/scores/{playerId:int}/holes")] HttpRequest req,
        int id,
        int playerId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("scorer", "admin");
        if (authError is not null) return authError;

        var body = await JsonSerializer.DeserializeAsync<SubmitHoleScoresRequest>(
            req.Body,
            JsonSerializerOptions.Web,
            cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var holeScores = body.HoleScores
            .Select(h => new HoleScoreInput(h.HoleNumber, h.GrossStrokes))
            .ToList();

        var command = new SubmitHoleScoresCommand(id, playerId, holeScores, userId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToOkResult();
    }

    [Function("FinalizeRound")]
    public async Task<IActionResult> FinalizeRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{id:int}/finalize")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new FinalizeRoundCommand(id, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreateRoundRequest(
        int SeasonId,
        int FlightId,
        int CourseId,
        DateOnly RoundDate,
        string? Notes,
        List<int> PlayerIds);

    private sealed record HoleScoreInputDto(int HoleNumber, int GrossStrokes);

    private sealed record SubmitHoleScoresRequest(List<HoleScoreInputDto> HoleScores);
}
