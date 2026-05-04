using GolfLeague.Application.Registrations.Commands;
using GolfLeague.Application.Registrations.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class RegistrationFunctions
{
    private readonly IMediator _mediator;

    public RegistrationFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetPendingRegistrations")]
    public async Task<IActionResult> GetPendingRegistrations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/registrations")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var result = await _mediator.Send(new GetPendingRegistrationsQuery(), cancellationToken);
        return result.ToOkResult();
    }

    [Function("ApproveRegistration")]
    public async Task<IActionResult> ApproveRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/registrations/{id}/approve")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var registrationId))
            return new BadRequestObjectResult(new { error = "Invalid registration ID." });

        var adminUserId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new ApproveRegistrationCommand(registrationId, adminUserId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("RejectRegistration")]
    public async Task<IActionResult> RejectRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/registrations/{id}/reject")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var registrationId))
            return new BadRequestObjectResult(new { error = "Invalid registration ID." });

        var body = await req.TryDeserializeAsync<RejectRequest>(cancellationToken);
        var adminUserId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new RejectRegistrationCommand(registrationId, body?.Reason, adminUserId), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record RejectRequest(string? Reason);
}
