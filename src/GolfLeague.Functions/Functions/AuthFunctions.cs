using GolfLeague.Application.Registrations.Commands;
using GolfLeague.Application.Registrations.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Auth-aware endpoints that bridge identity (Entra) with the app's player model.
/// </summary>
public sealed class AuthFunctions
{
    private readonly IMediator _mediator;

    public AuthFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns the calling user's registration/player status.
    /// Clients call this immediately after login to decide which screen to show.
    /// Response statuses: "none" | "pending" | "approved" | "rejected"
    /// </summary>
    [Function("GetMyStatus")]
    public async Task<IActionResult> GetMyStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/auth/me")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var entraObjectId = req.GetUserId();
        if (string.IsNullOrEmpty(entraObjectId))
            return new UnauthorizedResult();

        var result = await _mediator.Send(new GetMyRegistrationStatusQuery(entraObjectId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>
    /// Called by an authenticated user who wants to join the league.
    /// The request body pre-fills from Entra claims; the user can also supply a phone number.
    /// </summary>
    [Function("SubmitRegistration")]
    public async Task<IActionResult> SubmitRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/register")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var entraObjectId = req.GetUserId();
        if (string.IsNullOrEmpty(entraObjectId))
            return new UnauthorizedResult();

        var body = await req.TryDeserializeAsync<SubmitRegistrationRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var command = new SubmitRegistrationCommand(
            entraObjectId,
            body.FirstName,
            body.LastName,
            body.Email,
            body.Phone);

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
            return new ConflictObjectResult(new { error = result.Error });

        return new CreatedResult($"/api/v1/auth/me", result.Value);
    }

    private sealed record SubmitRegistrationRequest(
        string FirstName,
        string LastName,
        string Email,
        string? Phone);
}
