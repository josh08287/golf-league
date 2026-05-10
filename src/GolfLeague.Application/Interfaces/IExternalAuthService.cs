using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;

namespace GolfLeague.Application.Interfaces;

public interface IExternalAuthService
{
    /// <summary>
    /// Build the provider authorize URL and stash the PKCE verifier
    /// keyed by the returned state. Caller redirects the browser to AuthorizeUrl.
    /// </summary>
    Result<ExternalAuthStartDto> Start(string provider, string redirectUri);

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
