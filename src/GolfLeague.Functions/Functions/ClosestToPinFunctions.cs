using GolfLeague.Application.Admin;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Application.Rounds.Queries;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Closest-to-the-pin tracking for regular league rounds. The whole feature
/// is gated by the closest_to_pin_enabled feature flag.
/// </summary>
public sealed class ClosestToPinFunctions
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagRepository _featureFlags;

    public ClosestToPinFunctions(IMediator mediator, IFeatureFlagRepository featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    private async Task<bool> FeatureEnabledAsync(CancellationToken cancellationToken)
    {
        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.ClosestToPinEnabled, cancellationToken);
        return flag?.Enabled
            ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.ClosestToPinEnabled];
    }

    /// <summary>
    /// GET /v1/rounds/{id}/closest-to-pin — The round's par-3 holes with any
    /// recorded winners, plus the round's active participants for the picker.
    /// Any authenticated user may view.
    /// </summary>
    [Function("GetRoundClosestToPin")]
    public async Task<IActionResult> GetRoundClosestToPin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/rounds/{id:int}/closest-to-pin")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        if (!await FeatureEnabledAsync(cancellationToken))
            return new NotFoundObjectResult(new { error = "Closest-to-pin tracking is not enabled." });

        var result = await _mediator.Send(new GetRoundClosestToPinQuery(id), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new NotFoundObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// PUT /v1/rounds/{id}/closest-to-pin — Replace the round's closest-to-pin
    /// winners. Scorer/admin only.
    /// </summary>
    [Function("SetRoundClosestToPin")]
    public async Task<IActionResult> SetRoundClosestToPin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/rounds/{id:int}/closest-to-pin")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("scorer", "admin");
        if (authError is not null) return authError;

        if (!await FeatureEnabledAsync(cancellationToken))
            return new BadRequestObjectResult(new { error = "Closest-to-pin tracking is not enabled." });

        var body = await req.TryDeserializeAsync<SetClosestToPinRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var selections = body.Selections?
            .Select(s => new ClosestToPinSelection(s.HoleNumber, s.PlayerId))
            .ToList() ?? [];

        var result = await _mediator.Send(new SetClosestToPinWinnersCommand(id, selections, userId), cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        var updated = await _mediator.Send(new GetRoundClosestToPinQuery(id), cancellationToken);
        return updated.IsSuccess
            ? new OkObjectResult(new { data = updated.Value })
            : new BadRequestObjectResult(new { error = updated.Error });
    }

    private sealed record SetClosestToPinSelectionRequest(int HoleNumber, int? PlayerId);
    private sealed record SetClosestToPinRequest(List<SetClosestToPinSelectionRequest>? Selections);
}
