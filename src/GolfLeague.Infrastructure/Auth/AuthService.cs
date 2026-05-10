using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly AppDbContext _dbContext;
    private readonly IPlayerRepository _playerRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        AppDbContext dbContext,
        IPlayerRepository playerRepository,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _playerRepository = playerRepository;
        _logger = logger;
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return Result<AuthResponseDto>.Fail("An account with that email already exists.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            Role = PlayerRole.Player,
            CreatedAt = DateTime.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<AuthResponseDto>.Fail(errors);
        }

        // Try to link this new account to an existing Player by email match
        // (e.g., an admin pre-created the Player row before the user signed up).
        await TryLinkPlayerByEmailAsync(user, cancellationToken);

        var response = await IssueTokensAsync(user, cancellationToken);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Result<AuthResponseDto>.Fail("Invalid email or password.");

        if (await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponseDto>.Fail("Account is temporarily locked. Try again later.");

        if (!await _userManager.HasPasswordAsync(user))
        {
            // Account exists but has no password yet (bootstrap admin or
            // social-only). Tell the caller to use the password-reset flow.
            return Result<AuthResponseDto>.Fail(
                "This account has no password set. Use the password reset flow to set one.");
        }

        var ok = await _userManager.CheckPasswordAsync(user, password);
        if (!ok)
        {
            await _userManager.AccessFailedAsync(user);
            return Result<AuthResponseDto>.Fail("Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var response = await IssueTokensAsync(user, cancellationToken);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<AuthResponseDto>> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<AuthResponseDto>.Fail("Refresh token is required.");

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .AsTracking()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
            return Result<AuthResponseDto>.Fail("Invalid or expired refresh token.");

        // Rotate: revoke the used token and issue a new one.
        stored.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = stored.User!;
        var response = await IssueTokensAsync(user, cancellationToken);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<bool>> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<bool>.Ok(true);

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Ok(true);
    }

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return Result<CurrentUserDto>.Fail("User not found.");

        var hasPasskey = await _dbContext.UserPasskeys
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        var dto = new CurrentUserDto(
            user.Id,
            user.Email ?? string.Empty,
            user.Role.ToString().ToLowerInvariant(),
            user.PlayerId,
            hasPasskey,
            user.TotpEnabled);

        return Result<CurrentUserDto>.Ok(dto);
    }

    public async Task<AuthResponseDto> IssueAuthenticatedTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");
        return await IssueFullTokensAsync(user, cancellationToken);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var requiresMfa = user.Role == PlayerRole.Admin;
        var mfaEnrolled = user.TotpEnabled
            || await _dbContext.UserPasskeys.AnyAsync(p => p.UserId == user.Id, cancellationToken);

        if (requiresMfa)
        {
            // Return MFA-challenge token; no refresh token is issued until
            // MFA completes successfully.
            var challenge = _tokenService.IssueMfaChallengeToken(user);
            return new AuthResponseDto(
                AccessToken: challenge.Token,
                RefreshToken: string.Empty,
                AccessTokenExpiresAt: challenge.ExpiresAt,
                Role: user.Role.ToString().ToLowerInvariant(),
                UserId: user.Id,
                MfaRequired: true,
                MfaEnrollmentRequired: !mfaEnrolled);
        }

        return await IssueFullTokensAsync(user, cancellationToken);
    }

    private async Task<AuthResponseDto> IssueFullTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var access = _tokenService.IssueAccessToken(user);
        var refreshPlaintext = _tokenService.GenerateRefreshToken();
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(refreshPlaintext),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime),
        };
        _dbContext.RefreshTokens.Add(refresh);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponseDto(
            AccessToken: access.Token,
            RefreshToken: refreshPlaintext,
            AccessTokenExpiresAt: access.ExpiresAt,
            Role: user.Role.ToString().ToLowerInvariant(),
            UserId: user.Id,
            MfaRequired: false,
            MfaEnrollmentRequired: false);
    }

    private async Task TryLinkPlayerByEmailAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.Email)) return;

        var player = await _playerRepository.GetByEmailAsync(user.Email, cancellationToken);
        if (player is null || player.AppUserId is not null) return;

        player.AppUserId = user.Id;
        await _playerRepository.UpdateAsync(player, cancellationToken);

        user.PlayerId = player.Id;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation(
            "Linked AppUser {UserId} to existing Player {PlayerId} by email match",
            user.Id, player.Id);
    }
}
