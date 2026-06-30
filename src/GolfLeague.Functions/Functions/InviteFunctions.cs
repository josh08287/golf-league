using GolfLeague.Application.Common;
using GolfLeague.Application.Registrations.Commands;
using GolfLeague.Application.Registrations.Queries;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

public sealed class InviteFunctions
{
    private readonly IMediator _mediator;
    private readonly string _webBaseUrl;

    public InviteFunctions(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _webBaseUrl = config["WEB_BASE_URL"] ?? "http://localhost:5173";
    }

    /// <summary>GET /v1/admin/invites — list all invites (admin only)</summary>
    [Function("GetInvites")]
    public async Task<IActionResult> GetInvites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/invites")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);
        var result = await _mediator.Send(new GetInvitesQuery(_webBaseUrl, sort), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>POST /v1/admin/invites — create one or more invites (admin only)</summary>
    [Function("CreateInvites")]
    public async Task<IActionResult> CreateInvites(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/invites")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var body = await req.TryDeserializeAsync<CreateInvitesRequest>(cancellationToken);
        if (body is null || body.Emails.Count == 0)
            return new BadRequestObjectResult(new { error = "At least one email address is required." });

        var adminUserId = req.GetUserId() ?? "unknown";
        var command = new CreateInvitesCommand(
            body.Emails,
            adminUserId,
            _webBaseUrl,
            body.ExpiryDays ?? 7,
            body.Role ?? "player",
            body.PreLinkedPlayerId);
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>POST /v1/admin/invites/{id}/revoke — revoke a pending invite (admin only)</summary>
    [Function("RevokeInvite")]
    public async Task<IActionResult> RevokeInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/invites/{id}/revoke")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var inviteId))
            return new BadRequestObjectResult(new { error = "Invalid invite ID." });

        var adminUserId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new RevokeInviteCommand(inviteId, adminUserId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>DELETE /v1/admin/invites/{id} — delete an accepted, revoked, or expired invite (admin only)</summary>
    [Function("DeleteInvite")]
    public async Task<IActionResult> DeleteInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/invites/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!int.TryParse(id, out var inviteId))
            return new BadRequestObjectResult(new { error = "Invalid invite ID." });

        var adminUserId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new DeleteInviteCommand(inviteId, adminUserId), cancellationToken);
        return result.ToOkResult();
    }

    /// <summary>GET /v1/invites/{token} — fetch invite details by token (public — used by accept page)</summary>
    [Function("GetInviteByToken")]
    public async Task<IActionResult> GetInviteByToken(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/invites/{token}")] HttpRequest req,
        string token,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetInviteByTokenQuery(token, _webBaseUrl), cancellationToken);
        if (!result.IsSuccess)
            return new NotFoundObjectResult(new { error = result.Error });
        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>POST /v1/invites/{token}/accept — accept invite (must be authenticated)</summary>
    [Function("AcceptInvite")]
    public async Task<IActionResult> AcceptInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/invites/{token}/accept")] HttpRequest req,
        string token,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var appUserId))
            return new UnauthorizedResult();

        var body = await req.TryDeserializeAsync<AcceptInviteRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var command = new AcceptInviteCommand(token, appUserId, body.FirstName, body.LastName, body.Email, body.Phone);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return result.ToOkResult();

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>GET /v1/auth/me — returns player status for the calling user</summary>
    [Function("GetMyStatus")]
    public async Task<IActionResult> GetMyStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/auth/me")] HttpRequest req,
        ILogger log,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var appUserId))
            return new UnauthorizedResult();

        var result = await _mediator.Send(new GetMyStatusQuery(appUserId, req.GetLeagueId()), cancellationToken);
        return result.ToOkResult();
    }

    private sealed record CreateInvitesRequest(List<string> Emails, int? ExpiryDays, string? Role, int? PreLinkedPlayerId);
    private sealed record AcceptInviteRequest(string FirstName, string LastName, string Email, string? Phone);
}
