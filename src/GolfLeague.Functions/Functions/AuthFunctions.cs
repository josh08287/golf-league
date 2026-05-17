using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace GolfLeague.Functions.Functions;

public sealed class AuthFunctions
{
    private readonly IAuthService _authService;
    private readonly string _webBaseUrl;

    public AuthFunctions(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _webBaseUrl = configuration["WEB_BASE_URL"] ?? "http://localhost:5173";
    }

    [Function("Register")]
    public async Task<IActionResult> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/register")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<RegisterRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
            return new BadRequestObjectResult(new { error = "Email and password are required." });

        if (string.IsNullOrWhiteSpace(body.InviteToken))
            return new BadRequestObjectResult(new { error = "An invite is required to create an account." });

        var result = await _authService.RegisterAsync(
            body.Email,
            body.Password,
            body.FirstName ?? string.Empty,
            body.LastName ?? string.Empty,
            body.InviteToken,
            cancellationToken);

        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    [Function("Login")]
    public async Task<IActionResult> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/login")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<LoginRequest>(cancellationToken);
        if (body is null)
            return new BadRequestObjectResult(new { error = "Request body is required." });

        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
            return new BadRequestObjectResult(new { error = "Email and password are required." });

        var result = await _authService.LoginAsync(body.Email, body.Password, body.LeagueId, cancellationToken);
        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    [Function("Refresh")]
    public async Task<IActionResult> Refresh(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/refresh")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<RefreshRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.RefreshToken))
            return new BadRequestObjectResult(new { error = "Refresh token is required." });

        var result = await _authService.RefreshAsync(body.RefreshToken, body.LeagueId, cancellationToken);
        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// GET /v1/auth/me/leagues — returns all leagues the calling user is a member of.
    /// Used by the frontend to populate the league picker and determine auto-selection.
    /// </summary>
    [Function("GetMyLeagues")]
    public async Task<IActionResult> GetMyLeagues(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/auth/me/leagues")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new UnauthorizedResult();

        var result = await _authService.GetMyLeaguesAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return new NotFoundObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    [Function("Logout")]
    public async Task<IActionResult> Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/logout")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<RefreshRequest>(cancellationToken);
        var refreshToken = body?.RefreshToken ?? string.Empty;

        await _authService.LogoutAsync(refreshToken, cancellationToken);
        return new NoContentResult();
    }

    [Function("CurrentUser")]
    public async Task<IActionResult> CurrentUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/auth/current")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireAuthenticated();
        if (authError is not null) return authError;

        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new UnauthorizedResult();

        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return new NotFoundObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// POST /v1/auth/password-reset/request — public; emails a reset link.
    /// Always returns 200 even when the email doesn't match an account, so we
    /// don't leak account existence to enumeration attempts.
    /// </summary>
    [Function("RequestPasswordReset")]
    public async Task<IActionResult> RequestPasswordReset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password-reset/request")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<PasswordResetRequestBody>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.Email))
            return new BadRequestObjectResult(new { error = "Email is required." });

        await _authService.RequestPasswordResetAsync(body.Email, _webBaseUrl, cancellationToken);
        return new OkObjectResult(new { data = new { sent = true } });
    }

    /// <summary>
    /// POST /v1/auth/password-reset/confirm — public; consumes the token and sets a new password.
    /// </summary>
    [Function("ConfirmPasswordReset")]
    public async Task<IActionResult> ConfirmPasswordReset(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/password-reset/confirm")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<PasswordResetConfirmBody>(cancellationToken);
        if (body is null
            || string.IsNullOrWhiteSpace(body.Email)
            || string.IsNullOrWhiteSpace(body.Token)
            || string.IsNullOrWhiteSpace(body.NewPassword))
        {
            return new BadRequestObjectResult(new { error = "Email, token, and newPassword are required." });
        }

        var result = await _authService.ConfirmPasswordResetAsync(body.Email, body.Token, body.NewPassword, cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = new { reset = true } });
    }

    private sealed record RegisterRequest(string Email, string Password, string InviteToken, string? FirstName, string? LastName);
    private sealed record LoginRequest(string Email, string Password, int? LeagueId);
    private sealed record RefreshRequest(string RefreshToken, int? LeagueId);
    private sealed record PasswordResetRequestBody(string Email);
    private sealed record PasswordResetConfirmBody(string Email, string Token, string NewPassword);
}
