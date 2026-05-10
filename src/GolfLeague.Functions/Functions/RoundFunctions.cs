using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Enums;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

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
        int? seasonId = int.TryParse(req.Query["seasonId"], out var sid) ? sid : null;
        int? halfId = int.TryParse(req.Query["halfId"], out var hid) ? hid : null;
        var page = int.TryParse(req.Query["page"], out var p) ? p : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 20;

        var result = await _mediator.Send(new GetRoundsQuery(seasonId, halfId, page, pageSize), cancellationToken);
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
        var command = new CreateRoundCommand(
            body.HalfId,
            body.CourseId,
            body.ResolvedDate,
            body.ResolvedNineHoleSide,
            body.Notes,
            userId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToCreatedResult($"/api/v1/rounds/{result.Value?.Id}");
    }

    [Function("GenerateHalfSchedule")]
    public async Task<IActionResult> GenerateHalfSchedule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/halves/{halfId}/schedule")] HttpRequest req,
        string halfId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(halfId, out var hid))
            return new BadRequestObjectResult(new { error = "Invalid half ID." });

        var body = await req.TryDeserializeAsync<GenerateScheduleRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new GenerateHalfScheduleCommand(
            hid,
            body.CourseId,
            body.ResolvedWeekDates,
            body.ResolvedStartingSide,
            userId), cancellationToken);

        return result.ToCreatedResult($"/api/v1/halves/{hid}/schedule");
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

        try
        {
            var result = await _mediator.Send(new SubmitHoleScoresCommand(roundId, playerIdInt, holeScores, userId), cancellationToken);
            return result.ToOkResult();
        }
        catch (Exception ex)
        {
            // Surface the real cause so client-side errors aren't generic 500s.
            return new ObjectResult(new
            {
                error = ex.Message,
                type = ex.GetType().FullName,
                inner = ex.InnerException?.Message,
            })
            { StatusCode = 500 };
        }
    }

    [Function("SetParticipantSkipped")]
    public async Task<IActionResult> SetParticipantSkipped(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{id}/participants/{playerId}/skip")] HttpRequest req,
        string id,
        string playerId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("scorer", "admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId) || !int.TryParse(playerId, out var playerIdInt))
            return new BadRequestObjectResult(new { error = "Invalid ID." });

        var body = await req.TryDeserializeAsync<SetSkippedRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(
            new SetParticipantSkippedCommand(roundId, playerIdInt, body.Skipped, userId),
            cancellationToken);
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

    [Function("CancelRound")]
    public async Task<IActionResult> CancelRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{id}/cancel")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var roundId))
            return new BadRequestObjectResult(new { error = "Invalid round ID." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new CancelRoundCommand(roundId, userId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record SetSkippedRequest(bool Skipped);

    private sealed record CreateRoundRequest(
        int HalfId,
        int CourseId,
        string? ScheduledDate,
        string? RoundDate,
        string? Notes,
        string? NineHoleSide)
    {
        public DateOnly ResolvedDate => ScheduledDate is not null
            ? DateOnly.Parse(ScheduledDate)
            : RoundDate is not null
                ? DateOnly.Parse(RoundDate)
                : DateOnly.FromDateTime(DateTime.UtcNow);

        public NineHoleSide? ResolvedNineHoleSide => NineHoleSide?.ToLowerInvariant() switch
        {
            "back" or "back9" or "backnine" => Domain.Enums.NineHoleSide.Back,
            "front" or "front9" or "frontnine" => Domain.Enums.NineHoleSide.Front,
            _ => null,
        };
    }

    private sealed record GenerateScheduleRequest(
        int CourseId,
        List<string> WeekDates,
        string? StartingSide)
    {
        public List<DateOnly> ResolvedWeekDates => WeekDates.Select(DateOnly.Parse).ToList();

        public NineHoleSide ResolvedStartingSide => StartingSide?.ToLowerInvariant() switch
        {
            "back" or "back9" or "backnine" => Domain.Enums.NineHoleSide.Back,
            _ => Domain.Enums.NineHoleSide.Front,
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
