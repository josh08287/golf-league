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
}
