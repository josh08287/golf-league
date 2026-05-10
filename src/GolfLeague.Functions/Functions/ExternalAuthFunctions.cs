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

    private sealed record StartRequest(string RedirectUri);
    private sealed record CallbackRequest(string State, string Code, string RedirectUri);
}
