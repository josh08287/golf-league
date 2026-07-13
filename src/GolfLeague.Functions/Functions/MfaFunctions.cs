using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Functions.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class MfaFunctions
{
    private readonly IMfaService _mfaService;
    private readonly AuditWriter _auditWriter;

    public MfaFunctions(IMfaService mfaService, AuditWriter auditWriter)
    {
        _mfaService = mfaService;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// POST /v1/auth/mfa/totp/enroll — start TOTP enrollment.
    /// Accepts either a full access token or an MFA-challenge token; the
    /// AppUser id comes from the bearer's NameIdentifier claim.
    /// </summary>
    [Function("EnrollTotp")]
    public async Task<IActionResult> EnrollTotp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/mfa/totp/enroll")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new UnauthorizedResult();

        var result = await _mfaService.StartTotpEnrollmentAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        return new OkObjectResult(new { data = result.Value });
    }

    /// <summary>
    /// POST /v1/auth/mfa/totp/verify-enrollment — confirm enrollment by
    /// submitting a code from the authenticator app.
    /// </summary>
    [Function("VerifyTotpEnrollment")]
    public async Task<IActionResult> VerifyTotpEnrollment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/mfa/totp/verify-enrollment")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var userIdStr = req.GetUserId();
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new UnauthorizedResult();

        var body = await req.TryDeserializeAsync<CodeRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.Code))
            return new BadRequestObjectResult(new { error = "Code is required." });

        var result = await _mfaService.VerifyTotpEnrollmentAsync(userId, body.Code, cancellationToken);
        if (!result.IsSuccess)
            return new BadRequestObjectResult(new { error = result.Error });

        await _auditWriter.WriteAsync(
            "MfaEnrolled", "Session", userId.ToString(), userId.ToString(), cancellationToken: cancellationToken);

        return new OkObjectResult(new { data = new { enrolled = true } });
    }

    /// <summary>
    /// POST /v1/auth/mfa/totp/verify — second-factor step of admin login.
    /// Body carries the MFA-challenge token (issued by /auth/login when MFA
    /// is required) plus a 6-digit code; returns full access + refresh tokens.
    /// Public route — no bearer required on the request itself.
    /// </summary>
    [Function("VerifyTotpForLogin")]
    public async Task<IActionResult> VerifyTotpForLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/auth/mfa/totp/verify")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.TryDeserializeAsync<TotpLoginRequest>(cancellationToken);
        if (body is null || string.IsNullOrWhiteSpace(body.MfaToken) || string.IsNullOrWhiteSpace(body.Code))
            return new BadRequestObjectResult(new { error = "MFA token and code are required." });

        var result = await _mfaService.CompleteMfaWithTotpAsync(body.MfaToken, body.Code, cancellationToken);
        if (!result.IsSuccess)
            return new UnauthorizedObjectResult(new { error = result.Error });

        await _auditWriter.WriteAsync(
            "Login", "Session", result.Value!.UserId.ToString(), result.Value.UserId.ToString(),
            cancellationToken: cancellationToken);

        return new OkObjectResult(new { data = result.Value });
    }

    private sealed record CodeRequest(string Code);
    private sealed record TotpLoginRequest(string MfaToken, string Code);
}
