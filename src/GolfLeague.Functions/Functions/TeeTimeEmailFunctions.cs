using GolfLeague.Application.Admin;
using GolfLeague.Application.Rounds.Commands;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Manual tee time email actions for admins, gated by the
/// resend_tee_time_email_enabled feature flag.
/// </summary>
public sealed class TeeTimeEmailFunctions
{
    private readonly IMediator _mediator;
    private readonly IFeatureFlagRepository _featureFlags;

    public TeeTimeEmailFunctions(IMediator mediator, IFeatureFlagRepository featureFlags)
    {
        _mediator = mediator;
        _featureFlags = featureFlags;
    }

    /// <summary>
    /// POST /v1/rounds/{id}/tee-times/send-emails — Re-send the weekly tee
    /// time schedule email for the round to every participant. Admin only.
    /// Uses the same send pipeline as the Sunday autofill timer, so the
    /// per-league tee_time_email_enabled setting and per-player opt-outs
    /// still apply (a league with emails disabled reports 0 sent).
    /// </summary>
    [Function("ResendTeeTimeEmails")]
    public async Task<IActionResult> ResendTeeTimeEmails(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/rounds/{id:int}/tee-times/send-emails")] HttpRequest req,
        int id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var flag = await _featureFlags.GetAsync(KnownFeatureFlags.ResendTeeTimeEmailEnabled, cancellationToken);
        var enabled = flag?.Enabled ?? KnownFeatureFlags.Defaults[KnownFeatureFlags.ResendTeeTimeEmailEnabled];
        if (!enabled)
            return new BadRequestObjectResult(new { error = "Re-sending tee time emails is not enabled." });

        var result = await _mediator.Send(new SendTeeTimeScheduleEmailsCommand(id), cancellationToken);
        return result.IsSuccess
            ? new OkObjectResult(new { data = new { sent = result.Value } })
            : new BadRequestObjectResult(new { error = result.Error });
    }
}
