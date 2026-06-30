using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class ExternalAuthFunctions
{
    private readonly IExternalAuthService _externalAuth;

    public ExternalAuthFunctions(IExternalAuthService externalAuth)
    {
        _externalAuth = externalAuth;
    }

    /// <summary>
    /// POST /v1/auth/external/{provider}/start
    /// Body: { "redirectUri": "https://app.example.com/auth/callback" }
    /// Returns: { authorizeUrl, state } — client redirects to authorizeUrl.
    /// </summary>
    [Function("ExternalAuthStart")]
    public async Task<IActionResult> Start(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/external/{provider}/start")] HttpRequest req,
        string provider,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<StartRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.RedirectUri))
            return new BadRequestObjectResult(new { error = "redirectUri is required." });

        var result = _externalAuth.Start(provider.ToLowerInvariant(), body.RedirectUri, body.InviteToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// POST /v1/auth/external/{provider}/callback
    /// Body: { state, code, redirectUri }
    /// Returns: AuthResponseDto (full access + refresh tokens).
    /// </summary>
    [Function("ExternalAuthCallback")]
    public async Task<IActionResult> Callback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/external/{provider}/callback")] HttpRequest req,
        string provider,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<CallbackRequest>(cancellationToken);
        if (body is null
            || string.IsNullOrWhiteSpace(body.State)
            || string.IsNullOrWhiteSpace(body.Code)
            || string.IsNullOrWhiteSpace(body.RedirectUri))
        {
            return new BadRequestObjectResult(new { error = "state, code, and redirectUri are required." });
        }

        var result = await _externalAuth.CompleteAsync(
            provider.ToLowerInvariant(), body.State, body.Code, body.RedirectUri, cancellationToken);

        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// GET /v1/auth/external/{provider}/mobile-callback
    /// Google redirects here (https://) after the user consents.
    /// This relay immediately redirects the browser to the app's custom scheme
    /// so flutter_web_auth_2 can capture the code and state.
    /// Register https://golf-league-fn-g5vkqe.azurewebsites.net/api/v1/auth/external/google/mobile-callback
    /// as an Authorized redirect URI on the Google web OAuth client.
    /// </summary>
    [Function("ExternalAuthMobileCallback")]
    public IActionResult MobileCallback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/auth/external/{provider}/mobile-callback")] HttpRequest req,
        string provider)
    {
        var code  = req.Query["code"].FirstOrDefault();
        var state = req.Query["state"].FirstOrDefault();
        var error = req.Query["error"].FirstOrDefault();

        const string appScheme = "com.golfleague.app";

        // Chrome Custom Tabs blocks JavaScript navigation to custom app schemes
        // (no user gesture), so the redirect must be a real HTTP 302 — the
        // browser follows it and hands the deep link to the app.
        if (!string.IsNullOrEmpty(error))
            return new RedirectResult($"{appScheme}://auth?error={Uri.EscapeDataString(error)}");

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return new BadRequestObjectResult(new { error = "Missing code or state." });

        var deepLink = $"{appScheme}://auth?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}";
        return new RedirectResult(deepLink);
    }

    private sealed record StartRequest(string RedirectUri, string? InviteToken);
    private sealed record CallbackRequest(string State, string Code, string RedirectUri);
}
