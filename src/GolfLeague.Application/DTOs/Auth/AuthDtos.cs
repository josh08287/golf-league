namespace GolfLeague.Application.DTOs.Auth;

/// <summary>
/// Response from login / register / refresh. If MfaRequired is true the
/// caller has primary-factor proof but must satisfy a second factor before
/// using the returned tokens — in that case AccessToken is a short-lived
/// MFA-challenge token, not a full access token.
/// </summary>
public sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    IReadOnlyList<string> Roles,
    Guid UserId,
    bool MfaRequired,
    bool MfaEnrollmentRequired);

public sealed record CurrentUserDto(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    int? PlayerId,
    bool HasPasskey,
    bool HasTotp);
