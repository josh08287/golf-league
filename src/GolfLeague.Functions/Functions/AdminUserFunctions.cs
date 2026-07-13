using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

/// <summary>
/// Admin management for AppUser rows that aren't linked to a Player —
/// typically league administrators who don't compete. Player-linked rows
/// are managed via /api/v1/players.
/// </summary>
public sealed class AdminUserFunctions
{
    private readonly IAdminUserService _service;
    private readonly AuditWriter _auditWriter;

    public AdminUserFunctions(IAdminUserService service, AuditWriter auditWriter)
    {
        _service = service;
        _auditWriter = auditWriter;
    }

    [Function("ListAdminUsers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/users")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var users = await _service.ListAdminOnlyUsersAsync(cancellationToken);
        return new OkObjectResult(new { data = users });
    }

    [Function("UpdateAdminUserRoles")]
    public async Task<IActionResult> SetRoles(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/users/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!Guid.TryParse(id, out var userId))
            return new BadRequestObjectResult(new { error = "Invalid user id." });

        var body = await req.TryDeserializeAsync<RolesBody>(cancellationToken);
        if (body?.Roles is null)
            return new BadRequestObjectResult(new { error = "Body must include roles array." });

        var result = await _service.SetRolesAsync(userId, body.Roles, cancellationToken);
        if (!result.IsSuccess)
            return new ConflictObjectResult(new { error = result.Error });

        await _auditWriter.WriteAsync(
            "AdminUserRolesChanged", "AdminUser", userId.ToString(), req.GetUserId() ?? string.Empty,
            leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);

        return new OkObjectResult(new { data = result.Value });
    }

    [Function("ResetAdminUserMfa")]
    public async Task<IActionResult> ResetMfa(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{id}/reset-mfa")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!Guid.TryParse(id, out var userId))
            return new BadRequestObjectResult(new { error = "Invalid user id." });

        var result = await _service.ResetMfaAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        await _auditWriter.WriteAsync(
            "AdminUserMfaReset", "AdminUser", userId.ToString(), req.GetUserId() ?? string.Empty,
            leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);

        return new OkObjectResult(new { data = new { reset = true } });
    }

    [Function("AttachPlayerProfile")]
    public async Task<IActionResult> AttachPlayer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/users/{id}/attach-player")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!Guid.TryParse(id, out var userId))
            return new BadRequestObjectResult(new { error = "Invalid user id." });

        var body = await req.TryDeserializeAsync<AttachPlayerBody>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        var result = await _service.AttachPlayerProfileAsync(
            userId,
            body.FirstName ?? string.Empty,
            body.LastName ?? string.Empty,
            body.InitialHandicap,
            body.FlightId,
            cancellationToken);

        if (!result.IsSuccess)
            return new ConflictObjectResult(new { error = result.Error });

        await _auditWriter.WriteAsync(
            "AdminUserPlayerAttached", "AdminUser", userId.ToString(), req.GetUserId() ?? string.Empty,
            leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);

        return new OkObjectResult(new { data = result.Value });
    }

    [Function("DeleteAdminUser")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/users/{id}")] HttpRequest req,
        string id,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        if (!Guid.TryParse(id, out var userId))
            return new BadRequestObjectResult(new { error = "Invalid user id." });

        var result = await _service.DeleteAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return new ConflictObjectResult(new { error = result.Error });

        await _auditWriter.WriteAsync(
            "AdminUserDeleted", "AdminUser", userId.ToString(), req.GetUserId() ?? string.Empty,
            leagueId: req.GetLeagueId(), cancellationToken: cancellationToken);

        return new NoContentResult();
    }

    private sealed record RolesBody(string[] Roles);
    private sealed record AttachPlayerBody(string? FirstName, string? LastName, double InitialHandicap, int? FlightId);
}
