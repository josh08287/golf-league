using System.Security.Claims;
using GolfLeague.Domain.Entities;

namespace GolfLeague.Application.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Issues a normal access token with one "role" claim per assigned role.
    /// Used after primary login is complete and any required MFA has been
    /// satisfied. leagueId is the active league context for this session;
    /// isSuperAdmin bypasses all league-scoped authorization checks.
    /// </summary>
    AccessTokenResult IssueAccessToken(AppUser user, IEnumerable<string> roles, int? leagueId, bool isSuperAdmin);

    /// <summary>
    /// Issues a short-lived MFA-challenge token that proves the user
    /// passed the primary factor but has NOT satisfied MFA. The role
    /// claim is replaced with "mfa-pending" so the rest of the API
    /// will reject it.
    /// </summary>
    AccessTokenResult IssueMfaChallengeToken(AppUser user);

    /// <summary>
    /// Generates a fresh refresh-token plaintext. Caller is responsible
    /// for persisting only the hash via IAuthService.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Hashes a refresh-token plaintext for safe storage / lookup.
    /// </summary>
    string HashRefreshToken(string refreshToken);

    /// <summary>
    /// Validates an MFA-challenge token and returns the principal if valid.
    /// </summary>
    ClaimsPrincipal? ValidateMfaChallengeToken(string token);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
