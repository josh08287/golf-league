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

    public AdminUserFunctions(IAdminUserService service)
    {
        _service = service;
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

        return new OkObjectResult(new { data = new { reset = true } });
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

        return new NoContentResult();
    }

    private sealed record RolesBody(string[] Roles);
}
