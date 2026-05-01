using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GolfLeague.Functions.Helpers;

public static class HttpRequestExtensions
{
    public static int? GetPlayerId(this HttpRequest request)
    {
        var claim = request.HttpContext.User.FindFirst("extension_PlayerId")?.Value;
        if (int.TryParse(claim, out var id))
            return id;
        return null;
    }

    public static string? GetRole(this HttpRequest request)
        => request.HttpContext.User.FindFirst("extension_Role")?.Value;

    public static string? GetUserId(this HttpRequest request)
        => request.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? request.HttpContext.User.FindFirst("oid")?.Value
           ?? request.HttpContext.User.FindFirst("sub")?.Value;

    public static IActionResult? RequireRole(this HttpRequest request, params string[] allowedRoles)
    {
        var user = request.HttpContext.User;

        if (user.Identity is null || !user.Identity.IsAuthenticated)
            return new UnauthorizedResult();

        var role = user.FindFirst("extension_Role")?.Value;
        if (role is null || !allowedRoles.Contains(role))
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
}
