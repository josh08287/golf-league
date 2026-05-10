using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;

namespace GolfLeague.Application.Interfaces;

/// <summary>
/// Local authentication service: email/password + refresh-token flows.
/// Social login (Google/Facebook) and passkey flows are layered on top of
/// this in separate services.
/// </summary>
public interface IAuthService
{
    Task<Result<AuthResponseDto>> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponseDto>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result<AuthResponseDto>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<Result<CurrentUserDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a full access + refresh token pair for the given user, bypassing
    /// any MFA checks. Used by MFA-completion paths (TOTP, passkey) once the
    /// second factor has been verified.
    /// </summary>
    Task<AuthResponseDto> IssueAuthenticatedTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an Identity password-reset token for the given email and
    /// emails it to the user as a link to webBaseUrl/auth/reset-password.
    /// Always returns Ok (don't leak account existence). When email isn't
    /// configured the token is logged so the operator can bootstrap manually.
    /// </summary>
    Task<Result<bool>> RequestPasswordResetAsync(
        string email,
        string webBaseUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms the password reset using the Identity token and sets the new
    /// password.
    /// </summary>
    Task<Result<bool>> ConfirmPasswordResetAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
