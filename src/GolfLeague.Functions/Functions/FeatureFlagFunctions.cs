using GolfLeague.Application.Admin;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class FeatureFlagFunctions
{
    private readonly IMediator _mediator;

    public FeatureFlagFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// GET /v1/admin/feature-flags — Returns every known feature flag and its
    /// current global enablement. Super-admin only: flags apply across every
    /// league, not just the caller's own.
    /// </summary>
    [Function("GetFeatureFlags")]
    public async Task<IActionResult> GetFeatureFlags(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/feature-flags")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireSuperAdmin();
        if (authError is not null) return authError;

        var result = await _mediator.Send(new GetFeatureFlagsQuery(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// PUT /v1/admin/feature-flags/{key} — Enables or disables a feature flag
    /// application-wide. Super-admin only.
    /// </summary>
    [Function("UpdateFeatureFlag")]
    public async Task<IActionResult> UpdateFeatureFlag(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/feature-flags/{key}")] HttpRequest req,
        string key,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireSuperAdmin();
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<UpdateFeatureFlagRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new UpdateFeatureFlagCommand(key, body.Enabled, userId), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    /// <summary>
    /// GET /v1/feature-flags — Returns key → enabled for every known feature
    /// flag. Any authenticated user may call this; it exposes only booleans,
    /// letting the frontend decide whether to render a flagged feature's UI
    /// without needing admin access to the settings pages.
    /// </summary>
    [Function("GetFeatureFlagStates")]
    public async Task<IActionResult> GetFeatureFlagStates(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/feature-flags")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var result = await _mediator.Send(new GetFeatureFlagStatesQuery(), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = result.Value })
            : new BadRequestObjectResult(new { error = result.Error });
    }

    private sealed record UpdateFeatureFlagRequest(bool Enabled);
}
