using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

public sealed class TeeTimeFunctions
{
    private readonly ITeeTimeService _service;
    private readonly ITeeTimeAutofillService _autofill;
    private readonly IRoundRepository _rounds;
    private readonly ITeeTimeRepository _teeTimes;
    private readonly IMediator _mediator;
    private readonly ILogger<TeeTimeFunctions> _logger;

    public TeeTimeFunctions(
        ITeeTimeService service,
        ITeeTimeAutofillService autofill,
        IRoundRepository rounds,
        ITeeTimeRepository teeTimes,
        IMediator mediator,
        ILogger<TeeTimeFunctions> logger)
    {
        _service = service;
        _autofill = autofill;
        _rounds = rounds;
        _teeTimes = teeTimes;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// GET /v1/tee-times/next — convenience: resolve the next scheduled
    /// round and return its tee-time schedule. Saves the client a separate
    /// lookup. 404 if no round is scheduled.
    /// </summary>
    [Function("GetNextRoundTeeTimes")]
    public async Task<IActionResult> GetNext(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tee-times/next")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var easternToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TeeTimeSchedule.EasternTimeZone);
        var today = DateOnly.FromDateTime(easternToday);
        var roundId = await _service.ResolveNextRoundIdAsync(today, cancellationToken);
        if (roundId is null)
            return new NotFoundObjectResult(new { error = "No upcoming scheduled round." });

        var result = await _service.GetScheduleAsync(roundId.Value, req.GetPlayerId(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    [Function("GetRoundTeeTimes")]
    public async Task<IActionResult> GetForRound(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id:int}/tee-times")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var result = await _service.GetScheduleAsync(id, req.GetPlayerId(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new NotFoundObjectResult(new { error = result.Error });
    }

    [Function("JoinTeeTime")]
    public async Task<IActionResult> Join(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{roundId:int}/tee-times/{teeTimeId:int}/join")] HttpRequest req,
        int roundId,
        int teeTimeId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var result = await _service.JoinAsync(roundId, teeTimeId, playerId.Value, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new ConflictObjectResult(new { error = result.Error });
    }

    [Function("LeaveTeeTime")]
    public async Task<IActionResult> Leave(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{roundId:int}/tee-times/leave")] HttpRequest req,
        int roundId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var result = await _service.LeaveAsync(roundId, playerId.Value, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new ConflictObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// POST /v1/admin/rounds/{id}/tee-times/run-autofill — admin manual
    /// trigger. The timer fires this automatically on Sunday at noon ET; this
    /// endpoint exists so an admin can run autofill ahead of schedule
    /// (e.g., for testing or to recover from a missed timer run).
    /// </summary>
    [Function("RunTeeTimeAutofill")]
    public async Task<IActionResult> RunAutofill(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/rounds/{id:int}/tee-times/run-autofill")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var result = await _autofill.RunAsync(id, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// GET /v1/me/todays-tee-time — Returns today's tee time info for
    /// the authenticated player (if they have a round today with a tee time assignment).
    /// Returns 404 if no round today or player not assigned to a tee time.
    /// </summary>
    [Function("GetMyTodaysTeeTime")]
    public async Task<IActionResult> GetMyTodaysTeeTime(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me/todays-tee-time")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var easternToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TeeTimeSchedule.EasternTimeZone);
        var today = DateOnly.FromDateTime(easternToday);
        var result = await _mediator.Send(new GetMyTodaysTeeTimeQuery(playerId.Value, today), cancellationToken);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        if (result.Value is null)
            return new NotFoundObjectResult(new { error = "No tee time found for today." });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// GET /v1/tee-times/{id}/group-scorecard — Returns the complete scorecard
    /// for all players in a tee time group, including current hole scores.
    /// Any authenticated user can view; players in the group can submit scores.
    /// </summary>
    [Function("GetTeeTimeGroupScorecard")]
    public async Task<IActionResult> GetTeeTimeGroupScorecard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tee-times/{id:int}/group-scorecard")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var callingPlayerId = req.GetPlayerId();
        var result = await _mediator.Send(new GetTeeTimeGroupScorecardQuery(id, callingPlayerId), cancellationToken);

        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new NotFoundObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// POST /v1/tee-times/{id}/submit-scores — Submit scores for all players
    /// in a tee time group. Does NOT finalize the round; scores are pre-populated
    /// for admin review. Any player in the tee time group can submit.
    /// </summary>
    [Function("SubmitTeeTimeGroupScores")]
    public async Task<IActionResult> SubmitTeeTimeGroupScores(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tee-times/{id:int}/submit-scores")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var body = await req.TryDeserializeAsync<SubmitTeeTimeGroupScoresRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var playerScores = body.PlayerScores?.Select(p => new PlayerHoleScoresInput(
            p.PlayerId,
            p.HoleScores?.Select(h => new HoleScoreInput(h.HoleNumber, h.GrossStrokes, h.Putts, h.FirstPuttDistanceFeet, h.FairwayHit)).ToList() ?? [])).ToList() ?? [];

        var command = new SubmitTeeTimeGroupScoresCommand(id, playerId.Value, playerScores, userId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// PUT /v1/tee-times/{id}/holes/{holeNumber}/scores — Save scores for a
    /// single hole for all players in the group. Safe to call per-hole as the
    /// player advances; upserts so re-entry overwrites prior data for that hole.
    /// </summary>
    [Function("SaveTeeTimeHoleScores")]
    public async Task<IActionResult> SaveHoleScores(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/tee-times/{id:int}/holes/{holeNumber:int}/scores")] HttpRequest req,
        int id,
        int holeNumber,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var body = await req.TryDeserializeAsync<SaveHoleScoresRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var playerScores = body.PlayerScores?.Select(p => new PlayerHoleScoresInput(
            p.PlayerId,
            p.HoleScores?.Select(h => new HoleScoreInput(h.HoleNumber, h.GrossStrokes, h.Putts, h.FirstPuttDistanceFeet, h.FairwayHit)).ToList() ?? [])).ToList() ?? [];

        var command = new SaveTeeTimeHoleScoresCommand(id, playerId.Value, holeNumber, playerScores, userId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? new OkObjectResult(new { data = new { saved = true } })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    private sealed record HoleScoreInputDto(int HoleNumber, int GrossStrokes, int? Putts = null, double? FirstPuttDistanceFeet = null, bool? FairwayHit = null);
    private sealed record PlayerScoreInputDto(int PlayerId, List<HoleScoreInputDto>? HoleScores);
    private sealed record SubmitTeeTimeGroupScoresRequest(List<PlayerScoreInputDto>? PlayerScores);
    private sealed record SaveHoleScoresRequest(List<PlayerScoreInputDto>? PlayerScores);

    /// <summary>
    /// POST /v1/admin/rounds/{roundId}/tee-times/{teeTimeId}/participants/{participantId}
    /// Admin-only: Move a participant to a specific tee time slot, bypassing the cutoff check.
    /// If the participant is already in a slot, they are moved. Capacity checks still apply.
    /// </summary>
    [Function("AdminMoveParticipantToTeeTime")]
    public async Task<IActionResult> AdminMoveParticipant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/rounds/{roundId:int}/tee-times/{teeTimeId:int}/participants/{participantId:int}")] HttpRequest req,
        int roundId,
        int teeTimeId,
        int participantId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null)
            return new NotFoundObjectResult(new { error = "Round not found." });

        var participant = round.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null)
            return new NotFoundObjectResult(new { error = "Participant not found in this round." });

        var slot = await _teeTimes.GetByIdAsync(teeTimeId, cancellationToken);
        if (slot is null || slot.RoundId != roundId)
            return new NotFoundObjectResult(new { error = "Tee time slot not found in this round." });

        // Capacity check: exclude the participant if they're already in this slot
        var occupants = slot.Participants.Count(p => p.Id != participantId);
        if (occupants >= Domain.Services.TeeTimeSchedule.CapacityPerTeeTime)
            return new ConflictObjectResult(new { error = "That tee time is full." });

        if (participant.TeeTimeId != teeTimeId)
        {
            await _teeTimes.SetParticipantTeeTimeAsync(participantId, teeTimeId, cancellationToken);
        }

        var result = await _service.GetScheduleAsync(roundId, req.GetPlayerId(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// DELETE /v1/admin/rounds/{roundId}/tee-times/participants/{participantId}
    /// Admin-only: Remove a participant from their current tee time slot, bypassing the cutoff check.
    /// </summary>
    [Function("AdminRemoveParticipantFromTeeTime")]
    public async Task<IActionResult> AdminRemoveParticipant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/rounds/{roundId:int}/tee-times/participants/{participantId:int}")] HttpRequest req,
        int roundId,
        int participantId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null)
            return new NotFoundObjectResult(new { error = "Round not found." });

        var participant = round.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null)
            return new NotFoundObjectResult(new { error = "Participant not found in this round." });

        if (participant.TeeTimeId is not null)
        {
            await _teeTimes.SetParticipantTeeTimeAsync(participantId, null, cancellationToken);
        }

        var result = await _service.GetScheduleAsync(roundId, req.GetPlayerId(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// GET /v1/admin/rounds/{roundId}/participants
    /// Admin-only: Get all participants for a round with their current tee time assignments.
    /// Includes unassigned participants for admin tee time management.
    /// </summary>
    [Function("GetRoundParticipantsForAdmin")]
    public async Task<IActionResult> GetRoundParticipantsForAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/rounds/{roundId:int}/participants")] HttpRequest req,
        int roundId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null)
            return new NotFoundObjectResult(new { error = "Round not found." });

        var participants = round.Participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
            .Select(p => new
            {
                p.Id,
                p.PlayerId,
                p.Player.FullName,
                p.FlightId,
                p.Flight?.Name,
                p.HandicapIndex,
                p.CourseHandicap,
                p.TeeTimeId,
                TeeTimeNumber = p.TeeTime?.TeeTimeNumber,
                p.IsWithdrawn,
                p.SkippedWeek
            })
            .ToList();

        return new OkObjectResult(new { data = participants });
    }
}
