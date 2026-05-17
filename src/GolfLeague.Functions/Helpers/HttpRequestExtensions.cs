using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GolfLeague.Functions.Helpers;

public static class HttpRequestExtensions
{
    public static string? GetUserId(this HttpRequest request)
        => request.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? request.HttpContext.User.FindFirst("sub")?.Value;

    /// <summary>
    /// Returns the calling user's linked Player id from the "playerId" JWT claim.
    /// </summary>
    public static int? GetPlayerId(this HttpRequest request)
    {
        var raw = request.HttpContext.User.FindFirst("playerId")?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// Returns the active league id from the "leagueId" JWT claim.
    /// </summary>
    public static int? GetLeagueId(this HttpRequest request)
    {
        var raw = request.HttpContext.User.FindFirst("leagueId")?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// Returns true when the JWT carries the "superAdmin" claim.
    /// SuperAdmins bypass all league-scoped authorization.
    /// </summary>
    public static bool IsSuperAdmin(this HttpRequest request)
        => request.HttpContext.User.FindFirst("superAdmin")?.Value == "true";

    /// <summary>
    /// Requires the user to hold one of the given roles OR be a SuperAdmin.
    /// Returns null on success, an IActionResult on failure.
    /// </summary>
    public static IActionResult? RequireRole(this HttpRequest request, params string[] allowedRoles)
    {
        var user = request.HttpContext.User;

        if (user.Identity is null || !user.Identity.IsAuthenticated)
            return new UnauthorizedResult();

        // SuperAdmin bypasses all role checks
        if (request.IsSuperAdmin())
            return null;

        var userRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles" || c.Type == "role")
            .Select(c => c.Value?.ToLowerInvariant())
            .ToList();

        var hasRole = allowedRoles.Any(r => userRoles.Contains(r.ToLowerInvariant()));
        if (!hasRole)
            return new ObjectResult(new { error = "Forbidden: insufficient role." }) { StatusCode = 403 };

        return null;
    }

    /// <summary>
    /// Requires the caller to be a SuperAdmin. Returns null on success.
    /// </summary>
    public static IActionResult? RequireSuperAdmin(this HttpRequest request)
    {
        var user = request.HttpContext.User;
        if (user.Identity is null || !user.Identity.IsAuthenticated)
            return new UnauthorizedResult();
        if (!request.IsSuperAdmin())
            return new ObjectResult(new { error = "Forbidden: super-admin access required." }) { StatusCode = 403 };
        return null;
    }

    public static IActionResult? RequireAuthenticated(this HttpRequest request)
    {
        var user = request.HttpContext.User;
        if (user.Identity is null || !user.Identity.IsAuthenticated)
            return new UnauthorizedResult();
        return null;
    }

    public static async Task<T?> TryDeserializeAsync<T>(this HttpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonSerializerOptions.Web, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
