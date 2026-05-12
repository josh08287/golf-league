using System.Security.Claims;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using OtpNet;

namespace GolfLeague.Infrastructure.Auth;

public sealed class MfaService : IMfaService
{
    private const string TotpIssuer = "Capital Golf League";
    private const int TotpStep = 30;
    private const int TotpDigits = 6;

    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IAuthService _authService;

    public MfaService(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        IAuthService authService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _authService = authService;
    }

    public async Task<Result<TotpEnrollmentDto>> StartTotpEnrollmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<TotpEnrollmentDto>.Fail("User not found.");

        // Generate a fresh 160-bit secret each time enrollment is started.
        // Re-enrollment overwrites the previous unconfirmed secret; existing
        // confirmed TOTP stays valid until VerifyTotpEnrollmentAsync rotates it.
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        user.TotpSecret = secret;
        // Don't flip TotpEnabled until verify succeeds — start can be called
        // repeatedly to re-display the QR without invalidating an active TOTP.
        await _userManager.UpdateAsync(user);

        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? userId.ToString());
        var issuer = Uri.EscapeDataString(TotpIssuer);
        var uri = $"otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits={TotpDigits}&period={TotpStep}";

        return Result<TotpEnrollmentDto>.Ok(new TotpEnrollmentDto(secret, uri));
    }

    public async Task<Result<bool>> VerifyTotpEnrollmentAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrEmpty(user.TotpSecret))
            return Result<bool>.Fail("No pending TOTP enrollment. Start enrollment first.");

        if (!ValidateTotp(user.TotpSecret, code))
            return Result<bool>.Fail("Invalid code. Try again.");

        user.TotpEnabled = true;
        await _userManager.UpdateAsync(user);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<AuthResponseDto>> CompleteMfaWithTotpAsync(
        string mfaChallengeToken,
        string code,
        CancellationToken cancellationToken = default)
    {
        var principal = _tokenService.ValidateMfaChallengeToken(mfaChallengeToken);
        if (principal is null)
            return Result<AuthResponseDto>.Fail("Invalid or expired MFA challenge.");

        var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            return Result<AuthResponseDto>.Fail("Invalid MFA challenge subject.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.TotpEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return Result<AuthResponseDto>.Fail("TOTP is not enabled for this account.");

        if (!ValidateTotp(user.TotpSecret, code))
            return Result<AuthResponseDto>.Fail("Invalid code.");

        var response = await _authService.IssueAuthenticatedTokensAsync(userId, cancellationToken);
        return Result<AuthResponseDto>.Ok(response);
    }

    private static bool ValidateTotp(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        // Trim/space-tolerant: authenticator apps sometimes split as "123 456".
        code = code.Replace(" ", "").Trim();
        if (code.Length != TotpDigits) return false;

        var bytes = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(bytes, step: TotpStep, totpSize: TotpDigits);
        // ±1 step tolerance handles small clock skew between phone and server.
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }
}
