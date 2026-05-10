using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class AuthFunctions
{
    private readonly IAuthService _authService;

    public AuthFunctions(IAuthService authService)
    {
        _authService = authService;
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

        var result = await _authService.RegisterAsync(
            body.Email, body.Password, body.FirstName ?? string.Empty, body.LastName ?? string.Empty, cancellationToken);

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

        var result = await _authService.LoginAsync(body.Email, body.Password, cancellationToken);
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

        var result = await _authService.RefreshAsync(body.RefreshToken, cancellationToken);
        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

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

    private sealed record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
    private sealed record LoginRequest(string Email, string Password);
    private sealed record RefreshRequest(string RefreshToken);
}
