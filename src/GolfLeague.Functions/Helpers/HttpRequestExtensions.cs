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

    public static IActionResult? RequireRole(this HttpRequest request, params string[] allowedRoles)
    {
        var user = request.HttpContext.User;

        if (user.Identity is null || !user.Identity.IsAuthenticated)
            return new UnauthorizedResult();

        // Get all role claims (case-insensitive comparison)
        var userRoles = user.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
            .Select(c => c.Value?.ToLowerInvariant())
            .ToList();

        var hasRole = allowedRoles.Any(r => userRoles.Contains(r.ToLowerInvariant()));
        if (!hasRole)
            return new ObjectResult(new { error = "Forbidden: insufficient role." }) { StatusCode = 403 };

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
