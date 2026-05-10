using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Auth;

public sealed class ExternalAuthService : IExternalAuthService
{
    private static readonly TimeSpan FlowLifetime = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuthService _authService;
    private readonly IPlayerRepository _playerRepository;
    private readonly ILogger<ExternalAuthService> _logger;

    public ExternalAuthService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration config,
        UserManager<AppUser> userManager,
        IAuthService authService,
        IPlayerRepository playerRepository,
        ILogger<ExternalAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _config = config;
        _userManager = userManager;
        _authService = authService;
        _playerRepository = playerRepository;
        _logger = logger;
    }

    public Result<ExternalAuthStartDto> Start(string provider, string redirectUri)
    {
        if (!TryGetProvider(provider, out var p))
            return Result<ExternalAuthStartDto>.Fail($"Unknown provider: {provider}");
        if (string.IsNullOrWhiteSpace(redirectUri))
            return Result<ExternalAuthStartDto>.Fail("redirectUri is required.");

        var (clientId, _) = GetCredentials(provider);
        if (string.IsNullOrEmpty(clientId))
            return Result<ExternalAuthStartDto>.Fail($"{provider} OAuth is not configured on the server.");

        var state = GenerateUrlSafeToken(32);
        var verifier = GenerateUrlSafeToken(64);
        var challenge = ComputeS256(verifier);

        _cache.Set(CacheKey(provider, state), new PkceState(verifier, redirectUri), FlowLifetime);

        var url = p.BuildAuthorizeUrl(clientId, redirectUri, state, challenge);
        return Result<ExternalAuthStartDto>.Ok(new ExternalAuthStartDto(url, state));
    }

    public async Task<Result<AuthResponseDto>> CompleteAsync(
        string provider,
        string state,
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetProvider(provider, out var p))
            return Result<AuthResponseDto>.Fail($"Unknown provider: {provider}");

        if (!_cache.TryGetValue(CacheKey(provider, state), out PkceState? pkce) || pkce is null)
            return Result<AuthResponseDto>.Fail("Invalid or expired authorization flow.");

        _cache.Remove(CacheKey(provider, state));

        if (!string.Equals(pkce.RedirectUri, redirectUri, StringComparison.Ordinal))
            return Result<AuthResponseDto>.Fail("redirectUri mismatch.");

        var http = _httpClientFactory.CreateClient(provider);
        var (clientId, clientSecret) = GetCredentials(provider);
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return Result<AuthResponseDto>.Fail($"{provider} OAuth is not configured on the server.");

        // Exchange code for access token
        var tokenResponse = await p.ExchangeCodeAsync(http, code, redirectUri, pkce.Verifier, clientId, clientSecret, cancellationToken);
        if (tokenResponse is null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            return Result<AuthResponseDto>.Fail($"Failed to exchange code with {provider}.");

        // Fetch user profile
        var profile = await p.FetchProfileAsync(http, tokenResponse.AccessToken, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Email) || string.IsNullOrWhiteSpace(profile.ProviderUserId))
            return Result<AuthResponseDto>.Fail($"{provider} did not return enough profile info (email required).");

        // Find or create AppUser
        var user = await FindOrCreateUserAsync(provider, profile, cancellationToken);
        if (user is null)
            return Result<AuthResponseDto>.Fail("Failed to create or link the account.");

        var tokens = await _authService.IssueAuthenticatedTokensAsync(user.Id, cancellationToken);
        return Result<AuthResponseDto>.Ok(tokens);
    }

    private async Task<AppUser?> FindOrCreateUserAsync(string provider, ExternalProfile profile, CancellationToken cancellationToken)
    {
        // 1) Look up by Identity external-login link (provider + providerUserId)
        var byLogin = await _userManager.FindByLoginAsync(provider, profile.ProviderUserId);
        if (byLogin is not null) return byLogin;

        // 2) Fall back to email match — link to existing account if found.
        var byEmail = await _userManager.FindByEmailAsync(profile.Email);
        if (byEmail is not null)
        {
            var addLogin = await _userManager.AddLoginAsync(
                byEmail,
                new UserLoginInfo(provider, profile.ProviderUserId, provider));
            if (!addLogin.Succeeded)
                _logger.LogWarning("Failed to attach {Provider} login to existing user {Email}", provider, profile.Email);
            return byEmail;
        }

        // 3) Create a new AppUser. Role defaults to Player; admins must be
        //    promoted explicitly.
        var fresh = new AppUser
        {
            UserName = profile.Email,
            Email = profile.Email,
            EmailConfirmed = true, // social provider attested email
            Role = PlayerRole.Player,
            CreatedAt = DateTime.UtcNow,
        };
        var create = await _userManager.CreateAsync(fresh);
        if (!create.Succeeded)
        {
            _logger.LogError("Failed to create user from {Provider}: {Errors}",
                provider, string.Join("; ", create.Errors.Select(e => e.Description)));
            return null;
        }

        await _userManager.AddLoginAsync(fresh, new UserLoginInfo(provider, profile.ProviderUserId, provider));

        // Link an existing pre-created Player row by email match if available.
        var player = await _playerRepository.GetByEmailAsync(profile.Email, cancellationToken);
        if (player is not null && player.AppUserId is null)
        {
            player.AppUserId = fresh.Id;
            await _playerRepository.UpdateAsync(player, cancellationToken);
            fresh.PlayerId = player.Id;
            await _userManager.UpdateAsync(fresh);
        }

        return fresh;
    }

    private (string ClientId, string ClientSecret) GetCredentials(string provider) => provider switch
    {
        "google" => (_config["GOOGLE_CLIENT_ID"] ?? "", _config["GOOGLE_CLIENT_SECRET"] ?? ""),
        "facebook" => (_config["FACEBOOK_APP_ID"] ?? "", _config["FACEBOOK_APP_SECRET"] ?? ""),
        _ => ("", ""),
    };

    private static bool TryGetProvider(string name, out IOAuthProvider provider)
    {
        provider = name switch
        {
            "google" => new GoogleProvider(),
            "facebook" => new FacebookProvider(),
            _ => null!,
        };
        return provider is not null;
    }

    private static string CacheKey(string provider, string state) => $"oauth:{provider}:{state}";

    private static string GenerateUrlSafeToken(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string ComputeS256(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private sealed record PkceState(string Verifier, string RedirectUri);
}

internal sealed record TokenResponse(string AccessToken);
internal sealed record ExternalProfile(string ProviderUserId, string Email, string? Name);

internal interface IOAuthProvider
{
    string BuildAuthorizeUrl(string clientId, string redirectUri, string state, string codeChallengeS256);
    Task<TokenResponse?> ExchangeCodeAsync(HttpClient http, string code, string redirectUri, string verifier, string clientId, string clientSecret, CancellationToken cancellationToken);
    Task<ExternalProfile?> FetchProfileAsync(HttpClient http, string accessToken, CancellationToken cancellationToken);
}

internal sealed class GoogleProvider : IOAuthProvider
{
    public string BuildAuthorizeUrl(string clientId, string redirectUri, string state, string codeChallengeS256)
    {
        var qs = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["code_challenge"] = codeChallengeS256,
            ["code_challenge_method"] = "S256",
            ["access_type"] = "online",
            ["prompt"] = "select_account",
        };
        return "https://accounts.google.com/o/oauth2/v2/auth?" + Encode(qs);
    }

    public async Task<TokenResponse?> ExchangeCodeAsync(HttpClient http, string code, string redirectUri, string verifier, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = verifier,
        });
        using var resp = await http.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("access_token", out var at)) return null;
        return new TokenResponse(at.GetString() ?? "");
    }

    public async Task<ExternalProfile?> FetchProfileAsync(HttpClient http, string accessToken, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return new ExternalProfile(
            ProviderUserId: json.GetProperty("sub").GetString() ?? "",
            Email: json.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
            Name: json.TryGetProperty("name", out var n) ? n.GetString() : null);
    }

    private static string Encode(Dictionary<string, string?> qs) =>
        string.Join('&', qs.Where(kv => kv.Value is not null).Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
}

internal sealed class FacebookProvider : IOAuthProvider
{
    public string BuildAuthorizeUrl(string clientId, string redirectUri, string state, string codeChallengeS256)
    {
        var qs = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["scope"] = "email,public_profile",
            ["response_type"] = "code",
            ["code_challenge"] = codeChallengeS256,
            ["code_challenge_method"] = "S256",
        };
        return "https://www.facebook.com/v19.0/dialog/oauth?" + Encode(qs);
    }

    public async Task<TokenResponse?> ExchangeCodeAsync(HttpClient http, string code, string redirectUri, string verifier, string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var qs = new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["code"] = code,
            ["code_verifier"] = verifier,
        };
        var url = "https://graph.facebook.com/v19.0/oauth/access_token?" + Encode(qs);
        using var resp = await http.GetAsync(url, cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("access_token", out var at)) return null;
        return new TokenResponse(at.GetString() ?? "");
    }

    public async Task<ExternalProfile?> FetchProfileAsync(HttpClient http, string accessToken, CancellationToken cancellationToken)
    {
        var url = $"https://graph.facebook.com/v19.0/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}";
        using var resp = await http.GetAsync(url, cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        return new ExternalProfile(
            ProviderUserId: json.GetProperty("id").GetString() ?? "",
            Email: json.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "",
            Name: json.TryGetProperty("name", out var n) ? n.GetString() : null);
    }

    private static string Encode(Dictionary<string, string?> qs) =>
        string.Join('&', qs.Where(kv => kv.Value is not null).Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
}
