using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class RoundFunctions
{
    private readonly IMediator _mediator;
    private readonly IFlightRepository _flightRepository;

    public RoundFunctions(IMediator mediator, IFlightRepository flightRepository)
    {
        _mediator = mediator;
        _flightRepository = flightRepository;
    }

    [Function("GetRounds")]
    public async Task<IActionResult> GetRounds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        int? seasonId = int.TryParse(req.Query["seasonId"], out var sid) ? sid : null;
        var page = int.TryParse(req.Query["page"], out var p) ? p : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 20;

        var result = await _mediator.Send(new GetRoundsQuery(seasonId, page, pageSize), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetRound")]
    public async Task<IActionResult> GetRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var result = await _mediator.Send(new GetRoundQuery(roundId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("CreateRound")]
    public async Task<IActionResult> CreateRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateRoundRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var participants = (body.PlayerIds ?? []).Select(pid => new CreateRoundParticipantInput(pid)).ToList();

        int seasonId = body.SeasonId ?? 0;
        if (seasonId == 0)
        {
            var activeSeasonId = await _flightRepository.GetActiveSeasonIdAsync(cancellationToken);
            if (activeSeasonId is null)
                return new BadRequestObjectResult(new { error = "No active season found. Please specify a seasonId." });
            seasonId = activeSeasonId.Value;
        }

        var command = new CreateRoundCommand(
            seasonId,
            body.FlightId,
            body.CourseId,
            body.ResolvedDate,
            body.Notes,
            participants,
            userId,
            body.ResolvedRoundType,
            body.ResolvedNineHoleSide);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToCreatedResult($"/api/v1/rounds/{result.Value?.Id}");
    }

    [Function("GetPlayerScorecard")]
    public async Task<IActionResult> GetPlayerScorecard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id}/scores/{playerId}")] HttpRequest req,
        string id,
        string playerId,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId) || !int.TryParse(playerId, out var playerIdInt))
            return new BadRequestObjectResult(new { error = "Invalid ID." });

        var result = await _mediator.Send(new GetPlayerScorecardQuery(roundId, playerIdInt), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetPlayerScorecardByRoute")]
    public async Task<IActionResult> GetPlayerScorecardByRoute(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id}/scorecards/{playerId}")] HttpRequest req,
        string id,
        string playerId,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId) || !int.TryParse(playerId, out var playerIdInt))
            return new BadRequestObjectResult(new { error = "Invalid ID." });

        var result = await _mediator.Send(new GetPlayerScorecardQuery(roundId, playerIdInt), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetRoundScorecards")]
    public async Task<IActionResult> GetRoundScorecards(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id}/scorecards")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var result = await _mediator.Send(new GetRoundScorecardsQuery(roundId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("GetRoundParticipants")]
    public async Task<IActionResult> GetRoundParticipants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id}/participants")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var result = await _mediator.Send(new GetRoundParticipantsQuery(roundId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("SubmitHoleScores")]
    public async Task<IActionResult> SubmitHoleScores(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/rounds/{id}/scores/{playerId}/holes")] HttpRequest req,
        string id,
        string playerId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("scorer", "admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId) || !int.TryParse(playerId, out var playerIdInt))
            return new BadRequestObjectResult(new { error = "Invalid ID." });

        var body = await req.TryDeserializeAsync<SubmitHoleScoresRequest>(cancellationToken);

        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var holeScores = body.ResolvedScores.Select(h => new HoleScoreInput(h.HoleNumber, h.ResolvedStrokes)).ToList();
        var result = await _mediator.Send(new SubmitHoleScoresCommand(roundId, playerIdInt, holeScores, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("DeleteRound")]
    public async Task<IActionResult> DeleteRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/rounds/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeleteRoundCommand(roundId, userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("FinalizeRound")]
    public async Task<IActionResult> FinalizeRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{id}/finalize")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new FinalizeRoundCommand(roundId, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreateRoundRequest(
        int? SeasonId, int FlightId, int CourseId,
        string? ScheduledDate, string? RoundDate,
        string? Notes, List<int>? PlayerIds,
        string? RoundType, string? NineHoleSide)
    {
        public DateOnly ResolvedDate => ScheduledDate is not null
            ? DateOnly.Parse(ScheduledDate)
            : RoundDate is not null
                ? DateOnly.Parse(RoundDate)
                : DateOnly.FromDateTime(DateTime.UtcNow);

        public RoundType ResolvedRoundType => RoundType?.ToLowerInvariant() switch
        {
            "eighteenhole" or "18" or "18hole" => RoundType.EighteenHole,
            _ => RoundType.NineHole
        };

        public NineHoleSide ResolvedNineHoleSide => NineHoleSide?.ToLowerInvariant() switch
        {
            "back" or "back9" or "backnine" => NineHoleSide.Back,
            _ => NineHoleSide.Front
        };
    }

    private sealed record HoleScoreInputDto(int HoleNumber, int? GrossStrokes, int? GrossScore)
    {
        public int ResolvedStrokes => GrossStrokes ?? GrossScore ?? 0;
    }

    private sealed record SubmitHoleScoresRequest(List<HoleScoreInputDto>? HoleScores, List<HoleScoreInputDto>? Scores)
    {
        public List<HoleScoreInputDto> ResolvedScores => HoleScores ?? Scores ?? [];
    }
}
