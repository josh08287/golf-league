using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;

namespace GolfLeague.Application.Interfaces;

public interface IMfaService
{
    /// <summary>
    /// Generates a new TOTP secret for the user and stores it (unconfirmed)
    /// on the AppUser. Returns the secret + an otpauth:// URI for QR codes.
    /// The user must call VerifyTotpEnrollmentAsync with a valid code before
    /// TOTP is considered active.
    /// </summary>
    Task<Result<TotpEnrollmentDto>> StartTotpEnrollmentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms enrollment by checking the user-supplied code against the
    /// pending secret. On success, marks TotpEnabled = true.
    /// </summary>
    Task<Result<bool>> VerifyTotpEnrollmentAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a valid MFA-challenge token + 6-digit TOTP code for a full
    /// access + refresh token pair.
    /// </summary>
    Task<Result<AuthResponseDto>> CompleteMfaWithTotpAsync(
        string mfaChallengeToken,
        string code,
        CancellationToken cancellationToken = default);
}

public sealed record TotpEnrollmentDto(string Secret, string OtpAuthUri);
