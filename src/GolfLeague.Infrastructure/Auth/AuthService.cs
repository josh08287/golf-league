using System.Text;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs.Auth;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
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
    private readonly IInviteRepository _inviteRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly ILeagueRepository _leagueRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<AppUser> userManager,
        ITokenService tokenService,
        AppDbContext dbContext,
        IPlayerRepository playerRepository,
        IInviteRepository inviteRepository,
        IHandicapRepository handicapRepository,
        ILeagueRepository leagueRepository,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _playerRepository = playerRepository;
        _inviteRepository = inviteRepository;
        _handicapRepository = handicapRepository;
        _leagueRepository = leagueRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string inviteToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inviteToken))
            return Result<AuthResponseDto>.Fail("An invite is required to create an account.");

        var invite = await _inviteRepository.GetByTokenAsync(inviteToken, cancellationToken);
        var inviteFailure = ValidateInvite(invite, email);
        if (inviteFailure is not null)
            return Result<AuthResponseDto>.Fail(inviteFailure);

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            return Result<AuthResponseDto>.Fail("An account with that email already exists.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true, // invite holder has access to the email inbox
            CreatedAt = DateTime.UtcNow,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<AuthResponseDto>.Fail(errors);
        }

        await AddRoleAsync(user, invite!.Role);
        await ConsumeInviteAsync(invite, user, firstName, lastName, cancellationToken);

        var response = await IssueTokensAsync(user, cancellationToken);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(
        string email,
        string password,
        string? leagueSlug = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Result<AuthResponseDto>.Fail("Invalid email or password.");

        if (await _userManager.IsLockedOutAsync(user))
            return Result<AuthResponseDto>.Fail("Account is temporarily locked. Try again later.");

        if (!await _userManager.HasPasswordAsync(user))
        {
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

        var (leagueId, leagueRole) = await ResolveLeagueAsync(user, leagueSlug, cancellationToken);
        var response = await IssueTokensAsync(user, cancellationToken, leagueId, leagueRole);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<AuthResponseDto>> RefreshAsync(
        string refreshToken,
        string? leagueSlug = null,
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

        stored.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = stored.User!;
        var (leagueId, leagueRole) = await ResolveLeagueAsync(user, leagueSlug, cancellationToken);
        var response = await IssueTokensAsync(user, cancellationToken, leagueId, leagueRole);
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
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<CurrentUserDto>.Fail("User not found.");

        var hasPasskey = await _dbContext.UserPasskeys
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        var normalized = roles.Select(r => r.ToLowerInvariant()).ToList();

        var dto = new CurrentUserDto(
            user.Id,
            user.Email ?? string.Empty,
            normalized,
            user.PlayerId,
            hasPasskey,
            user.TotpEnabled,
            user.IsSuperAdmin);

        return Result<CurrentUserDto>.Ok(dto);
    }

    public async Task<AuthResponseDto> IssueAuthenticatedTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found.");
        return await IssueTokensAsync(user, cancellationToken, leagueId: null, leagueRole: null);
    }

    public async Task<Result<bool>> RequestPasswordResetAsync(
        string email,
        string webBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result<bool>.Fail("Email is required.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            _logger.LogInformation("Password reset requested for unknown email {Email}", email);
            return Result<bool>.Ok(true);
        }

        var rawToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var urlSafeToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));

        var trimmedBase = (webBaseUrl ?? string.Empty).TrimEnd('/');
        var link = $"{trimmedBase}/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={urlSafeToken}";

        try
        {
            await _emailService.SendPasswordResetAsync(user.Email!, link, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
        }

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> ConfirmPasswordResetAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return Result<bool>.Fail("Email, token, and new password are required.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Result<bool>.Fail("Invalid reset link.");

        string rawToken;
        try
        {
            rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch
        {
            return Result<bool>.Fail("Invalid reset link.");
        }

        var result = await _userManager.ResetPasswordAsync(user, rawToken, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Fail(errors);
        }

        var existing = await _dbContext.RefreshTokens
            .AsTracking()
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var rt in existing) rt.RevokedAt = DateTime.UtcNow;
        if (existing.Count > 0) await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// Validate that the given invite is good for the supplied email and is
    /// still pending/non-expired. Returns null on success, an error message
    /// to surface on failure.
    /// </summary>
    internal static string? ValidateInvite(PlayerInvite? invite, string email)
    {
        if (invite is null) return "Invite not found.";
        if (invite.Status == InviteStatus.Revoked) return "This invite has been revoked.";
        if (invite.Status == InviteStatus.Accepted) return "This invite has already been used.";
        if (invite.ExpiresAt < DateTime.UtcNow) return "This invite has expired.";
        if (!string.Equals(invite.Email, email, StringComparison.OrdinalIgnoreCase))
            return "This invite was sent to a different email address.";
        return null;
    }

    /// <summary>
    /// Consume an invite: link the AppUser, create the Player row (if there
    /// isn't one already linked), seed an initial handicap, mark the invite
    /// accepted.
    /// </summary>
    internal async Task ConsumeInviteAsync(
        PlayerInvite invite,
        AppUser user,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var existingPlayer = await _playerRepository.GetByAppUserIdAsync(user.Id, cancellationToken);
        Player? preLink = null;
        if (existingPlayer is null && invite.PreLinkedPlayerId is int preId)
        {
            preLink = await _playerRepository.GetByIdAsync(preId, cancellationToken);
            _logger.LogInformation(
                "Invite {InviteId} pre-link lookup: PreLinkedPlayerId={PreId}, Found={Found}, AppUserId={AppUserId}",
                invite.Id, preId, preLink is not null, preLink?.AppUserId);
            if (preLink is not null && preLink.AppUserId is not null)
            {
                // Pre-linked player got linked elsewhere between invite creation
                // and acceptance — fall through to the email-match / new path.
                preLink = null;
            }
        }

        Player player;
        if (existingPlayer is not null)
        {
            player = existingPlayer;
        }
        else if (preLink is not null)
        {
            // Admin chose this specific Player when issuing the invite.
            // Honors the pre-attach even if emails don't match — admin
            // explicitly opted in.
            preLink.AppUserId = user.Id;
            preLink.FirstName = firstName;
            preLink.LastName = lastName;
            preLink.Email = user.Email;
            await _playerRepository.UpdateAsync(preLink, cancellationToken);
            player = preLink;
            _logger.LogInformation(
                "Linked AppUser {UserId} to pre-attached Player {PlayerId} from invite {InviteId}",
                user.Id, preLink.Id, invite.Id);
        }
        else
        {
            // Look for an admin-pre-created Player row by email and adopt it.
            var byEmail = await _playerRepository.GetByEmailAsync(user.Email!, cancellationToken);
            if (byEmail is not null && byEmail.AppUserId is null)
            {
                byEmail.AppUserId = user.Id;
                byEmail.FirstName = firstName;
                byEmail.LastName = lastName;
                byEmail.Email = user.Email; // keep player email in sync with the linked account
                await _playerRepository.UpdateAsync(byEmail, cancellationToken);
                player = byEmail;
            }
            else
            {
                player = new Player
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = user.Email!,
                    IsActive = true,
                    AppUserId = user.Id,
                };
                await _playerRepository.AddAsync(player, cancellationToken);
                await _handicapRepository.AddAsync(new Handicap
                {
                    PlayerId = player.Id,
                    HandicapIndex = 0.0,
                    EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    Source = HandicapSource.Initial,
                }, cancellationToken);
            }
        }

        user.PlayerId = player.Id;
        await _userManager.UpdateAsync(user);

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByAppUserId = user.Id;
        invite.PlayerId = player.Id;
        await _inviteRepository.UpdateAsync(invite, cancellationToken);
    }

    /// <summary>
    /// Ensure a single role is present on the AppUser. Idempotent.
    /// </summary>
    internal async Task AddRoleAsync(AppUser user, PlayerRole role)
    {
        var name = role.ToString().ToLowerInvariant();
        if (!await _userManager.IsInRoleAsync(user, name))
        {
            var result = await _userManager.AddToRoleAsync(user, name);
            if (!result.Succeeded)
            {
                _logger.LogError(
                    "Failed to add role {Role} to user {UserId}: {Errors}",
                    name, user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    /// <summary>
    /// Resolves the league context for token issuance. If the user is a SuperAdmin
    /// they always bypass league membership checks. Otherwise looks up the league
    /// by slug and returns the user's role in that league (or null if not a member).
    /// </summary>
    private async Task<(int? leagueId, string? leagueRole)> ResolveLeagueAsync(
        AppUser user,
        string? leagueSlug,
        CancellationToken cancellationToken)
    {
        if (user.IsSuperAdmin)
        {
            // SuperAdmin can operate across all leagues; no specific leagueId needed in token.
            if (!string.IsNullOrWhiteSpace(leagueSlug))
            {
                var league = await _leagueRepository.GetBySlugAsync(leagueSlug, cancellationToken);
                return (league?.Id, "admin");
            }
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(leagueSlug))
            return (null, null);

        var targetLeague = await _leagueRepository.GetBySlugAsync(leagueSlug, cancellationToken);
        if (targetLeague is null)
            return (null, null);

        var membership = await _leagueRepository.GetMembershipAsync(targetLeague.Id, user.Id, cancellationToken);
        return membership is not null
            ? (targetLeague.Id, membership.Role.ToString().ToLowerInvariant())
            : (null, null);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(
        AppUser user,
        CancellationToken cancellationToken,
        int? leagueId = null,
        string? leagueRole = null)
    {
        var roles = (await _userManager.GetRolesAsync(user))
            .Select(r => r.ToLowerInvariant())
            .ToList();

        var requiresMfa = false;
        var mfaEnrolled = user.TotpEnabled
            || await _dbContext.UserPasskeys.AnyAsync(p => p.UserId == user.Id, cancellationToken);

        if (requiresMfa)
        {
            var challenge = _tokenService.IssueMfaChallengeToken(user);
            return new AuthResponseDto(
                AccessToken: challenge.Token,
                RefreshToken: string.Empty,
                AccessTokenExpiresAt: challenge.ExpiresAt,
                Roles: roles,
                UserId: user.Id,
                MfaRequired: true,
                MfaEnrollmentRequired: !mfaEnrolled);
        }

        return await IssueFullTokensAsync(user, cancellationToken, roles, leagueId, leagueRole);
    }

    private async Task<AuthResponseDto> IssueFullTokensAsync(
        AppUser user,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? cachedRoles = null,
        int? leagueId = null,
        string? leagueRole = null)
    {
        var roles = cachedRoles
            ?? (await _userManager.GetRolesAsync(user)).Select(r => r.ToLowerInvariant()).ToList();

        // For league-scoped tokens, use the league membership role if available.
        // This overrides the global Identity role for authorization within the league context.
        var effectiveRoles = leagueRole is not null
            ? [leagueRole]
            : roles;

        var access = _tokenService.IssueAccessToken(user, effectiveRoles, leagueId, user.IsSuperAdmin);
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
            Roles: effectiveRoles,
            UserId: user.Id,
            MfaRequired: false,
            MfaEnrollmentRequired: false);
    }
}
