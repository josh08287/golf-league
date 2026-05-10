using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;

namespace GolfLeague.Application.Interfaces;

public interface IPasskeyService
{
    /// <summary>
    /// Build registration options for the calling user. Returned JSON is passed
    /// straight to navigator.credentials.create() on the client.
    /// </summary>
    Task<Result<string>> StartRegistrationAsync(Guid userId, string? friendlyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify the attestation response and persist the new passkey.
    /// </summary>
    Task<Result<bool>> CompleteRegistrationAsync(Guid userId, string attestationJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Build assertion options for a known user (used as MFA second factor
    /// after primary login). The caller supplies the MFA-challenge token.
    /// </summary>
    Task<Result<string>> StartMfaAssertionAsync(string mfaChallengeToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify the assertion and, on success, exchange for full tokens.
    /// </summary>
    Task<Result<AuthResponseDto>> CompleteMfaAssertionAsync(
        string mfaChallengeToken,
        string assertionJson,
        CancellationToken cancellationToken = default);
}
