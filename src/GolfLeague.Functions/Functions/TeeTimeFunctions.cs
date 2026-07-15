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
    private readonly AuditWriter _auditWriter;
    private readonly ILogger<TeeTimeFunctions> _logger;

    public TeeTimeFunctions(
        ITeeTimeService service,
        ITeeTimeAutofillService autofill,
        IRoundRepository rounds,
        ITeeTimeRepository teeTimes,
        IMediator mediator,
        AuditWriter auditWriter,
        ILogger<TeeTimeFunctions> logger)
    {
        _service = service;
        _autofill = autofill;
        _rounds = rounds;
        _teeTimes = teeTimes;
        _mediator = mediator;
        _auditWriter = auditWriter;
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

    /// <summary>
    /// GET /v1/rounds/{roundId}/tee-times/substitutes/available — Lists
    /// substitute-pool players not yet seated in this round, for the
    /// "add a substitute" picker.
    /// </summary>
    [Function("GetAvailableSubstitutesForRound")]
    public async Task<IActionResult> GetAvailableSubstitutes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{roundId:int}/tee-times/substitutes/available")] HttpRequest req,
        int roundId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var result = await _mediator.Send(new GetAvailableSubstitutesQuery(roundId), cancellationToken);
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
    /// POST /v1/rounds/{roundId}/tee-times/participants/{otherParticipantId}/switch
    /// Self-service: the calling player swaps tee-time slots with another
    /// participant in the round, moving into that participant's group (and
    /// vice versa). Not gated by the sign-up window — a straight swap never
    /// changes slot occupancy, so it's safe any time before the round.
    /// </summary>
    [Function("SwitchTeeTimeParticipant")]
    public async Task<IActionResult> SwitchParticipant(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{roundId:int}/tee-times/participants/{otherParticipantId:int}/switch")] HttpRequest req,
        int roundId,
        int otherParticipantId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var result = await _service.SwapAsync(roundId, playerId.Value, otherParticipantId, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new ConflictObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// POST /v1/rounds/{roundId}/tee-times/{teeTimeId}/join-as-substitute
    /// A substitute-pool player claims a seat in a tee-time slot themselves,
    /// only allowed up to as many substitutes as players who've skipped.
    /// </summary>
    [Function("JoinTeeTimeAsSubstitute")]
    public async Task<IActionResult> JoinAsSubstitute(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{roundId:int}/tee-times/{teeTimeId:int}/join-as-substitute")] HttpRequest req,
        int roundId,
        int teeTimeId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var result = await _service.JoinAsSubstituteAsync(roundId, teeTimeId, playerId.Value, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new ConflictObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// POST /v1/rounds/{roundId}/tee-times/substitutes/{substitutePlayerId}
    /// Caller adds a substitute to their own tee-time slot, only allowed up
    /// to as many substitutes as players who've skipped the round.
    /// </summary>
    [Function("AddSubstituteToTeeTime")]
    public async Task<IActionResult> AddSubstitute(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{roundId:int}/tee-times/substitutes/{substitutePlayerId:int}")] HttpRequest req,
        int roundId,
        int substitutePlayerId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var result = await _service.AddSubstituteAsync(roundId, playerId.Value, substitutePlayerId, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new ConflictObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// DELETE /v1/rounds/{roundId}/tee-times/substitutes/{substituteParticipantId}
    /// Caller removes a substitute they added to their own tee-time slot.
    /// </summary>
    [Function("RemoveSubstituteFromTeeTime")]
    public async Task<IActionResult> RemoveSubstitute(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/rounds/{roundId:int}/tee-times/substitutes/{substituteParticipantId:int}")] HttpRequest req,
        int roundId,
        int substituteParticipantId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var result = await _service.RemoveSubstituteAsync(roundId, playerId.Value, substituteParticipantId, cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new ConflictObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// POST /v1/admin/rounds/{id}/tee-times/run-autofill — admin manual
    /// trigger. The timer fires this automatically at the league's configured
    /// sign-up cutoff time (default 6pm ET) the day before each round; this
    /// endpoint exists so an admin can run autofill ahead of schedule (e.g.,
    /// for testing or to recover from a missed timer run).
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
    /// POST /v1/rounds/{id}/tee-times/me/skip — Player marks themselves as
    /// skipping (or un-skipping) a round before auto-fill runs. The audit log
    /// records their own userId, distinguishing a self-skip from an admin skip.
    /// </summary>
    [Function("SkipMyWeek")]
    public async Task<IActionResult> SkipMyWeek(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{id:int}/tee-times/me/skip")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var body = await req.TryDeserializeAsync<SetSkippedRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var round = await _rounds.GetByIdAsync(id, cancellationToken);
        if (round is null)
            return new NotFoundObjectResult(new { error = "Round not found." });

        var easternToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TeeTimeSchedule.EasternTimeZone));
        if (round.RoundDate < easternToday)
            return new BadRequestObjectResult(new { error = "This round has already started." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetParticipantSkippedCommand(id, playerId.Value, body.Skipped, userId), cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        var schedule = await _service.GetScheduleAsync(id, playerId, cancellationToken);
        return schedule.IsSuccess
            ? new OkObjectResult(new { data = schedule.Value })
            : new BadRequestObjectResult(new { error = schedule.Error });
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
        var confirmedOverwrites = body.ConfirmedOverwrites?.Select(c => new ConfirmedOverwrite(c.PlayerId, c.HoleNumber)).ToList();

        var command = new SubmitTeeTimeGroupScoresCommand(id, playerId.Value, playerScores, userId, confirmedOverwrites);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        if (result.Value!.Conflicts.Count > 0)
            return new ConflictObjectResult(new { error = "Score conflicts detected.", conflicts = result.Value.Conflicts });

        return new OkObjectResult(new { data = result.Value.Result });
    }

    /// <summary>
    /// POST /v1/tee-times/{id}/participants/{playerId}/skip — Mark a player in
    /// the tee time group as skipped (or un-skip them). Any authenticated player
    /// in the group can call this; the command validates group membership.
    /// </summary>
    [Function("SetTeeTimeParticipantSkipped")]
    public async Task<IActionResult> SetTeeTimeParticipantSkipped(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tee-times/{id:int}/participants/{playerId:int}/skip")] HttpRequest req,
        int id,
        int playerId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var callingPlayerId = req.GetPlayerId();
        if (callingPlayerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        var body = await req.TryDeserializeAsync<SetSkippedRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        // Verify the caller is in this tee time group
        var teeTime = await _teeTimes.GetByIdAsync(id, cancellationToken);
        if (teeTime is null)
            return new NotFoundObjectResult(new { error = "Tee time not found." });

        var callerInGroup = teeTime.Participants.Any(p => p.PlayerId == callingPlayerId.Value && !p.IsWithdrawn);
        if (!callerInGroup)
            return new ForbidResult();

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new SetParticipantSkippedCommand(teeTime.RoundId, playerId, body.Skipped, userId), cancellationToken);

        return result.IsSuccess
            ? new OkObjectResult(new { data = new { skipped = body.Skipped } })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    private sealed record SetSkippedRequest(bool Skipped);

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
        var confirmedOverwrites = body.ConfirmedOverwrites?.Select(c => new ConfirmedOverwrite(c.PlayerId, c.HoleNumber)).ToList();

        var command = new SaveTeeTimeHoleScoresCommand(id, playerId.Value, holeNumber, playerScores, userId, confirmedOverwrites);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        if (result.Value!.Conflicts.Count > 0)
            return new ConflictObjectResult(new { error = "Score conflicts detected.", conflicts = result.Value.Conflicts });

        return new OkObjectResult(new { data = new { saved = true } });
    }

    /// <summary>
    /// POST /v1/tee-times/{id}/scorecard-ocr — Parses an uploaded scorecard
    /// photo (multipart/form-data, field name "image") into per-player hole
    /// scores for the caller to confirm/edit. Read entirely into memory and
    /// discarded once this returns — never written to disk or blob storage.
    /// Gated by the scorecard_ocr_enabled feature flag.
    /// </summary>
    [Function("ScanTeeTimeScorecard")]
    public async Task<IActionResult> ScanScorecard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/tee-times/{id:int}/scorecard-ocr")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var playerId = req.GetPlayerId();
        if (playerId is null)
            return new ConflictObjectResult(new { error = "Your account isn't linked to a player profile." });

        if (!req.HasFormContentType)
            return new BadRequestObjectResult(new { error = "An image file is required (multipart field \"image\")." });

        var form = await req.ReadFormAsync(cancellationToken);
        if (form.Files.Count == 0)
            return new BadRequestObjectResult(new { error = "An image file is required (multipart field \"image\")." });

        var file = form.Files["image"];
        if (file is null)
            return new BadRequestObjectResult(new { error = "An image file is required (multipart field \"image\")." });
        const long maxImageBytes = 15 * 1024 * 1024;
        if (file.Length == 0 || file.Length > maxImageBytes)
            return new BadRequestObjectResult(new { error = "Image must be non-empty and under 15 MB." });

        byte[] imageBytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream, cancellationToken);
            imageBytes = stream.ToArray();
        }

        var command = new ParseScorecardImageCommand(id, playerId.Value, imageBytes);
        try
        {
            var result = await _mediator.Send(command, cancellationToken);

            return result.IsSuccess
                ? new OkObjectResult(new { data = result.Value })
                : new BadRequestObjectResult(new { error = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scorecard OCR failed for tee time {TeeTimeId}, player {PlayerId}.", id, playerId.Value);
            return new ObjectResult(new { error = "Scorecard scanning failed. Please try again or enter scores manually." })
            {
                StatusCode = 500,
            };
        }
    }

    private sealed record HoleScoreInputDto(int HoleNumber, int GrossStrokes, int? Putts = null, double? FirstPuttDistanceFeet = null, bool? FairwayHit = null);
    private sealed record PlayerScoreInputDto(int PlayerId, List<HoleScoreInputDto>? HoleScores);
    private sealed record ConfirmedOverwriteDto(int PlayerId, int HoleNumber);
    private sealed record SubmitTeeTimeGroupScoresRequest(List<PlayerScoreInputDto>? PlayerScores, List<ConfirmedOverwriteDto>? ConfirmedOverwrites = null);
    private sealed record SaveHoleScoresRequest(List<PlayerScoreInputDto>? PlayerScores, List<ConfirmedOverwriteDto>? ConfirmedOverwrites = null);

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
            await _auditWriter.WriteAsync(
                "AdminTeeTimeMove", "Round", roundId.ToString(), req.GetUserId() ?? string.Empty,
                leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);
        }

        var result = await _service.GetScheduleAsync(roundId, req.GetPlayerId(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// POST /v1/admin/rounds/{roundId}/tee-times/participants/{participantId}/swap/{otherParticipantId}
    /// Admin-only: Swap two participants' tee-time slots in one step. Works
    /// even when both slots are full since capacity never changes — each
    /// participant simply takes the other's seat. Also works when one side
    /// is unassigned (equivalent to a move, but expressed as a swap by the
    /// drag-and-drop UI when dropping onto an occupied seat).
    /// </summary>
    [Function("AdminSwapTeeTimeParticipants")]
    public async Task<IActionResult> AdminSwapParticipants(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/rounds/{roundId:int}/tee-times/participants/{participantId:int}/swap/{otherParticipantId:int}")] HttpRequest req,
        int roundId,
        int participantId,
        int otherParticipantId,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (participantId == otherParticipantId)
            return new BadRequestObjectResult(new { error = "Cannot swap a participant with themselves." });

        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null)
            return new NotFoundObjectResult(new { error = "Round not found." });

        var participant = round.Participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null)
            return new NotFoundObjectResult(new { error = "Participant not found in this round." });

        var otherParticipant = round.Participants.FirstOrDefault(p => p.Id == otherParticipantId);
        if (otherParticipant is null)
            return new NotFoundObjectResult(new { error = "Participant not found in this round." });

        await _teeTimes.SwapParticipantTeeTimesAsync(participantId, otherParticipantId, cancellationToken);
        await _auditWriter.WriteAsync(
            "AdminTeeTimeSwap", "Round", roundId.ToString(), req.GetUserId() ?? string.Empty,
            leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);

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
            await _auditWriter.WriteAsync(
                "AdminTeeTimeRemove", "Round", roundId.ToString(), req.GetUserId() ?? string.Empty,
                leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);
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
