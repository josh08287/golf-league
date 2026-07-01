using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;

namespace GolfLeague.Application.Interfaces;

public interface IExternalAuthService
{
    /// <summary>
    /// Build the provider authorize URL and stash the PKCE verifier
    /// keyed by the returned state. Caller redirects the browser to AuthorizeUrl.
    /// </summary>
    /// <param name="inviteToken">
    /// Optional invite token. When supplied (the user started from an invite
    /// link), it is carried through the flow so a fresh social sign-up is
    /// authorized by the token rather than requiring the provider's email to
    /// match the invited address.
    /// </param>
    Result<ExternalAuthStartDto> Start(string provider, string redirectUri, string? inviteToken = null, string? envUrl = null);

    /// <summary>
    /// Exchange a provider auth code for tokens, fetch the user profile,
    /// create-or-link the AppUser, and return our own access/refresh tokens.
    /// </summary>
    Task<Result<AuthResponseDto>> CompleteAsync(
        string provider,
        string state,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalAuthStartDto(string AuthorizeUrl, string State);
