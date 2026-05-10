using System.Security.Claims;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Auth;

/// <summary>
/// WebAuthn (passkey) registration + assertion for use as an admin MFA
/// second factor. Password-less passkey login isn't wired up yet — adding
/// that is a straightforward extension once this is verified end-to-end.
/// </summary>
public sealed class PasskeyService : IPasskeyService
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly IFido2 _fido2;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        IFido2 fido2,
        UserManager<AppUser> userManager,
        AppDbContext dbContext,
        IAuthService authService,
        ITokenService tokenService,
        IMemoryCache cache,
        ILogger<PasskeyService> logger)
    {
        _fido2 = fido2;
        _userManager = userManager;
        _dbContext = dbContext;
        _authService = authService;
        _tokenService = tokenService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<string>> StartRegistrationAsync(
        Guid userId,
        string? friendlyName,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<string>.Fail("User not found.");

        var existing = await _dbContext.UserPasskeys
            .Where(p => p.UserId == userId)
            .Select(p => p.CredentialId)
            .ToListAsync(cancellationToken);

        var excludeCredentials = existing
            .Select(id => new PublicKeyCredentialDescriptor(Convert.FromBase64String(id)))
            .ToList();

        var fidoUser = new Fido2User
        {
            Id = userId.ToByteArray(),
            Name = user.Email ?? user.UserName ?? userId.ToString(),
            DisplayName = user.Email ?? user.UserName ?? "User",
        };

        var authSelection = new AuthenticatorSelection
        {
            RequireResidentKey = false,
            UserVerification = UserVerificationRequirement.Required,
        };

        var options = _fido2.RequestNewCredential(
            fidoUser,
            excludeCredentials,
            authSelection,
            AttestationConveyancePreference.None);

        // Cache the options keyed by user — the client doesn't carry it back,
        // we re-read it on Complete to verify the challenge.
        _cache.Set(RegCacheKey(userId), new RegistrationContext(options, friendlyName), ChallengeLifetime);

        return Result<string>.Ok(options.ToJson());
    }

    public async Task<Result<bool>> CompleteRegistrationAsync(
        Guid userId,
        string attestationJson,
        CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue(RegCacheKey(userId), out RegistrationContext? ctx) || ctx is null)
            return Result<bool>.Fail("Registration challenge expired. Start enrollment again.");

        _cache.Remove(RegCacheKey(userId));

        AuthenticatorAttestationRawResponse rawResponse;
        try
        {
            rawResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationJson)
                ?? throw new InvalidOperationException("null response");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse attestation response");
            return Result<bool>.Fail("Malformed attestation response.");
        }

        try
        {
            // Verify the attestation and ensure the credential ID isn't already
            // registered to a different user.
            IsCredentialIdUniqueToUserAsyncDelegate isUnique = async (args, ct) =>
            {
                var id = Convert.ToBase64String(args.CredentialId);
                return !await _dbContext.UserPasskeys.AnyAsync(p => p.CredentialId == id, ct);
            };

            var result = await _fido2.MakeNewCredentialAsync(rawResponse, ctx.Options, isUnique, cancellationToken: cancellationToken);

            var passkey = new UserPasskey
            {
                UserId = userId,
                CredentialId = Convert.ToBase64String(result.Result!.CredentialId),
                PublicKey = result.Result.PublicKey,
                SignCount = result.Result.Counter,
                AaGuid = result.Result.Aaguid,
                Name = string.IsNullOrWhiteSpace(ctx.FriendlyName) ? "Passkey" : ctx.FriendlyName!,
                CreatedAt = DateTime.UtcNow,
            };
            _dbContext.UserPasskeys.Add(passkey);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result<bool>.Ok(true);
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning(ex, "Passkey attestation verification failed for user {UserId}", userId);
            return Result<bool>.Fail($"Attestation verification failed: {ex.Message}");
        }
    }

    public async Task<Result<string>> StartMfaAssertionAsync(
        string mfaChallengeToken,
        CancellationToken cancellationToken = default)
    {
        var userId = ExtractUserIdFromMfaChallenge(mfaChallengeToken);
        if (userId is null)
            return Result<string>.Fail("Invalid or expired MFA challenge.");

        var credentialIds = await _dbContext.UserPasskeys
            .Where(p => p.UserId == userId.Value)
            .Select(p => p.CredentialId)
            .ToListAsync(cancellationToken);

        if (credentialIds.Count == 0)
            return Result<string>.Fail("No passkeys registered.");

        var allowed = credentialIds
            .Select(id => new PublicKeyCredentialDescriptor(Convert.FromBase64String(id)))
            .ToList();

        var options = _fido2.GetAssertionOptions(
            allowed,
            UserVerificationRequirement.Required);

        _cache.Set(AssertionCacheKey(userId.Value), options, ChallengeLifetime);
        return Result<string>.Ok(options.ToJson());
    }

    public async Task<Result<AuthResponseDto>> CompleteMfaAssertionAsync(
        string mfaChallengeToken,
        string assertionJson,
        CancellationToken cancellationToken = default)
    {
        var userId = ExtractUserIdFromMfaChallenge(mfaChallengeToken);
        if (userId is null)
            return Result<AuthResponseDto>.Fail("Invalid or expired MFA challenge.");

        if (!_cache.TryGetValue(AssertionCacheKey(userId.Value), out AssertionOptions? options) || options is null)
            return Result<AuthResponseDto>.Fail("Assertion challenge expired. Start MFA again.");

        _cache.Remove(AssertionCacheKey(userId.Value));

        AuthenticatorAssertionRawResponse rawResponse;
        try
        {
            rawResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionJson)
                ?? throw new InvalidOperationException("null response");
        }
        catch
        {
            return Result<AuthResponseDto>.Fail("Malformed assertion response.");
        }

        var credentialId = Convert.ToBase64String(rawResponse.Id);
        var passkey = await _dbContext.UserPasskeys
            .AsTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.CredentialId == credentialId, cancellationToken);
        if (passkey is null)
            return Result<AuthResponseDto>.Fail("Unknown passkey credential.");

        try
        {
            IsUserHandleOwnerOfCredentialIdAsync isOwner = (args, _) =>
            {
                var providedUserId = new Guid(args.UserHandle);
                return Task.FromResult(providedUserId == userId.Value);
            };

            var result = await _fido2.MakeAssertionAsync(
                rawResponse,
                options,
                passkey.PublicKey,
                passkey.SignCount,
                isOwner,
                cancellationToken: cancellationToken);

            passkey.SignCount = result.Counter;
            passkey.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var tokens = await _authService.IssueAuthenticatedTokensAsync(userId.Value, cancellationToken);
            return Result<AuthResponseDto>.Ok(tokens);
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning(ex, "Passkey assertion verification failed for user {UserId}", userId);
            return Result<AuthResponseDto>.Fail($"Assertion verification failed: {ex.Message}");
        }
    }

    private Guid? ExtractUserIdFromMfaChallenge(string token)
    {
        var principal = _tokenService.ValidateMfaChallengeToken(token);
        var sub = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static string RegCacheKey(Guid userId) => $"passkey-reg:{userId}";
    private static string AssertionCacheKey(Guid userId) => $"passkey-asn:{userId}";

    private sealed record RegistrationContext(CredentialCreateOptions Options, string? FriendlyName);
}
