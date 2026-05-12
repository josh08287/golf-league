using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

public sealed class TeeTimeFunctions
{
    private readonly ITeeTimeService _service;
    private readonly ITeeTimeAutofillService _autofill;
    private readonly ILogger<TeeTimeFunctions> _logger;

    public TeeTimeFunctions(
        ITeeTimeService service,
        ITeeTimeAutofillService autofill,
        ILogger<TeeTimeFunctions> logger)
    {
        _service = service;
        _autofill = autofill;
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

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
}
