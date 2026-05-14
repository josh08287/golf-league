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

        var result = _externalAuth.Start(provider.ToLowerInvariant(), body.RedirectUri);
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

        if (!string.IsNullOrEmpty(error))
        {
            var errorDeepLink = $"{appScheme}://auth?error={Uri.EscapeDataString(error)}";
            var errorHtml = $"""
                <!DOCTYPE html>
                <html>
                <head><meta charset="utf-8"><title>Sign-in error</title></head>
                <body>
                <script>window.location = '{errorDeepLink}';</script>
                </body>
                </html>
                """;
            return new ContentResult { Content = errorHtml, ContentType = "text/html", StatusCode = 200 };
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return new BadRequestObjectResult(new { error = "Missing code or state." });

        var deepLink = $"{appScheme}://auth?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}";
        var html = $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"><title>Signing you in...</title></head>
            <body>
            <p>Signing you in, please wait...</p>
            <script>window.location = '{deepLink}';</script>
            </body>
            </html>
            """;
        return new ContentResult { Content = html, ContentType = "text/html", StatusCode = 200 };
    }

    private sealed record StartRequest(string RedirectUri);
    private sealed record CallbackRequest(string State, string Code, string RedirectUri);
}
