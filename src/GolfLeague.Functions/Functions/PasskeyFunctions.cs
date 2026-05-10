using System.IO;
using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class PasskeyFunctions
{
    private readonly IPasskeyService _passkeyService;

    public PasskeyFunctions(IPasskeyService passkeyService)
    {
        _passkeyService = passkeyService;
    }

    /// <summary>
    /// POST /v1/auth/passkey/registration/options
    /// Returns WebAuthn create options as JSON (passed to navigator.credentials.create()).
    /// Requires an authenticated bearer (either full token or MFA-challenge).
    /// </summary>
    [Function("PasskeyRegistrationOptions")]
    public async Task<IActionResult> RegistrationOptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/passkey/registration/options")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new UnauthorizedResult();

        var body = await req.TryDeserializeAsync<RegistrationOptionsRequest>(cancellationToken);
        var result = await _passkeyService.StartRegistrationAsync(userId, body?.FriendlyName, cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        // Fido2NetLib already returns canonical JSON — pass it through unwrapped.
        return new ContentResult { Content = result.Value, ContentType = "application/json", StatusCode = 200 };
    }

    /// <summary>
    /// POST /v1/auth/passkey/registration/complete
    /// Body: raw AuthenticatorAttestationRawResponse JSON from the client.
    /// </summary>
    [Function("PasskeyRegistrationComplete")]
    public async Task<IActionResult> RegistrationComplete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/passkey/registration/complete")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new UnauthorizedResult();

        using var reader = new StreamReader(req.Body);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
            return new BadRequestObjectResult(new { error = "Attestation response body is required." });

        var result = await _passkeyService.CompleteRegistrationAsync(userId, raw, cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = new { registered = true } });
    }

    /// <summary>
    /// POST /v1/auth/passkey/mfa/options
    /// Body: { mfaToken } — returns assertion options for the user identified
    /// by the MFA-challenge token.
    /// </summary>
    [Function("PasskeyMfaOptions")]
    public async Task<IActionResult> MfaOptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/passkey/mfa/options")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<MfaOptionsRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.MfaToken))
            return new BadRequestObjectResult(new { error = "mfaToken is required." });

        var result = await _passkeyService.StartMfaAssertionAsync(body.MfaToken, cancellationToken);
        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

        return new ContentResult { Content = result.Value, ContentType = "application/json", StatusCode = 200 };
    }

    /// <summary>
    /// POST /v1/auth/passkey/mfa/verify
    /// Body: { mfaToken, assertion } — verifies the assertion and on success
    /// returns full access + refresh tokens.
    /// </summary>
    [Function("PasskeyMfaVerify")]
    public async Task<IActionResult> MfaVerify(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/passkey/mfa/verify")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<MfaVerifyRequest>(cancellationToken);
        if (body is null
            || string.IsNullOrWhiteSpace(body.MfaToken)
            || string.IsNullOrWhiteSpace(body.Assertion))
        {
            return new BadRequestObjectResult(new { error = "mfaToken and assertion are required." });
        }

        var result = await _passkeyService.CompleteMfaAssertionAsync(body.MfaToken, body.Assertion, cancellationToken);
        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    private sealed record RegistrationOptionsRequest(string? FriendlyName);
    private sealed record MfaOptionsRequest(string MfaToken);
    private sealed record MfaVerifyRequest(string MfaToken, string Assertion);
}
