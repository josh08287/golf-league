using System.Text;
using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
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
        // The token authorizes registration; the user may use a different email
        // than the invite was addressed to (it becomes their account email, and
        // is adopted onto the pre-linked player in ConsumeInviteAsync).
        var inviteFailure = ValidateInvite(invite, email, requireEmailMatch: false);
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

        var leagueRole = invite.Role.ToString().ToLowerInvariant();
        var response = await IssueTokensAsync(user, cancellationToken, invite.LeagueId, leagueRole);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(
        string email,
        string password,
        int? leagueId = null,
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

        var (resolvedLeagueId, leagueRole) = await ResolveLeagueAsync(user, leagueId, cancellationToken);
        var response = await IssueTokensAsync(user, cancellationToken, resolvedLeagueId, leagueRole);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<AuthResponseDto>> RefreshAsync(
        string refreshToken,
        int? leagueId = null,
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
        var (resolvedLeagueId, leagueRole) = await ResolveLeagueAsync(user, leagueId, cancellationToken);
        var response = await IssueTokensAsync(user, cancellationToken, resolvedLeagueId, leagueRole);
        return Result<AuthResponseDto>.Ok(response);
    }

    public async Task<Result<UserLeaguesDto>> GetMyLeaguesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<UserLeaguesDto>.Fail("User not found.");

        if (user.IsSuperAdmin)
        {
            var all = await _leagueRepository.GetAllAsync(cancellationToken);
            var dtos = all.Select(l => new UserLeagueDto(l.Id, l.Name, l.Slug, "admin")).ToList();
            return Result<UserLeaguesDto>.Ok(new UserLeaguesDto(dtos, IsSuperAdmin: true));
        }

        var memberships = await _leagueRepository.GetMembershipsForUserAsync(userId, cancellationToken);
        var memberDtos = memberships
            .Select(m => new UserLeagueDto(m.LeagueId, m.League.Name, m.League.Slug, m.Role.ToString().ToLowerInvariant()))
            .ToList();
        return Result<UserLeaguesDto>.Ok(new UserLeaguesDto(memberDtos, IsSuperAdmin: false));
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
        int? leagueId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Result<CurrentUserDto>.Fail("User not found.");

        var hasPasskey = await _dbContext.UserPasskeys
            .AnyAsync(p => p.UserId == userId, cancellationToken);

        var roles = await _userManager.GetRolesAsync(user);
        var normalized = roles.Select(r => r.ToLowerInvariant()).ToList();

        var player = leagueId.HasValue
            ? await _playerRepository.GetByAppUserIdAsync(userId, leagueId.Value, cancellationToken)
            : null;

        var dto = new CurrentUserDto(
            user.Id,
            user.Email ?? string.Empty,
            normalized,
            player?.Id,
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
        var (leagueId, leagueRole) = await ResolveLeagueAsync(user, leagueId: null, cancellationToken);
        return await IssueTokensAsync(user, cancellationToken, leagueId, leagueRole);
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
    /// <param name="requireEmailMatch">
    /// When true, the supplied <paramref name="email"/> must match the invite's
    /// address. Token-based registration passes false: the invite token is the
    /// authorization, so the user may sign up with any email (which then becomes
    /// their account/player email). The social path keeps this true because it
    /// locates the invite *by* the provider email.
    /// </param>
    internal static string? ValidateInvite(PlayerInvite? invite, string email, bool requireEmailMatch = true)
    {
        if (invite is null) return "Invite not found.";
        if (invite.Status == InviteStatus.Revoked) return "This invite has been revoked.";
        if (invite.Status == InviteStatus.Accepted) return "This invite has already been used.";
        if (invite.ExpiresAt < DateTime.UtcNow) return "This invite has expired.";
        if (requireEmailMatch && !string.Equals(invite.Email, email, StringComparison.OrdinalIgnoreCase))
            return "This invite was sent to a different email address.";
        return null;
    }

    /// <summary>
    /// Consume an invite: link the AppUser, create the Player row for this
    /// invite's league (if the user doesn't already have one there — a user
    /// may hold separate Player rows in other leagues, which this leaves
    /// untouched), seed an initial handicap, mark the invite accepted.
    /// </summary>
    internal async Task ConsumeInviteAsync(
        PlayerInvite invite,
        AppUser user,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var existingPlayer = await _playerRepository.GetByAppUserIdAsync(user.Id, invite.LeagueId, cancellationToken);
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
            // No pre-linked player — create membership only; do not auto-link
            // or create a player profile.
            await _leagueRepository.AddMembershipAsync(new LeagueMembership
            {
                LeagueId = invite.LeagueId,
                UserId = user.Id,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow,
            }, cancellationToken);

            invite.Status = InviteStatus.Accepted;
            invite.AcceptedAt = DateTime.UtcNow;
            invite.AcceptedByAppUserId = user.Id;
            invite.PreLinkedPlayer = null;
            await _inviteRepository.UpdateAsync(invite, cancellationToken);
            return;
        }

        await _leagueRepository.AddMembershipAsync(new LeagueMembership
        {
            LeagueId = invite.LeagueId,
            UserId = user.Id,
            Role = invite.Role,
            JoinedAt = DateTime.UtcNow,
        }, cancellationToken);

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedAt = DateTime.UtcNow;
        invite.AcceptedByAppUserId = user.Id;
        invite.PlayerId = player.Id;
        // Detach the loaded navigation so EF doesn't attempt a second UPDATE on
        // the player row that was already saved above.
        invite.PreLinkedPlayer = null;
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
    /// Resolves league scope for token issuance by leagueId.
    /// SuperAdmin with a leagueId gets a league-scoped token with admin role.
    /// Regular users must be a member of the requested league.
    /// When leagueId is null and the user belongs to exactly one league,
    /// that league is auto-selected.
    /// </summary>
    private async Task<(int? leagueId, string? leagueRole)> ResolveLeagueAsync(
        AppUser user,
        int? leagueId,
        CancellationToken cancellationToken)
    {
        if (user.IsSuperAdmin)
        {
            if (leagueId.HasValue)
            {
                var league = await _leagueRepository.GetByIdAsync(leagueId.Value, cancellationToken);
                return league is not null ? (league.Id, "admin") : (null, null);
            }
            return (null, null);
        }

        // Auto-select when no leagueId requested and user has exactly one membership.
        if (!leagueId.HasValue)
        {
            var memberships = await _leagueRepository.GetMembershipsForUserAsync(user.Id, cancellationToken);
            if (memberships.Count == 1)
                return (memberships[0].LeagueId, memberships[0].Role.ToString().ToLowerInvariant());
            return (null, null);
        }

        var membership = await _leagueRepository.GetMembershipAsync(leagueId.Value, user.Id, cancellationToken);
        return membership is not null
            ? (leagueId.Value, membership.Role.ToString().ToLowerInvariant())
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

        var player = leagueId.HasValue
            ? await _playerRepository.GetByAppUserIdAsync(user.Id, leagueId.Value, cancellationToken)
            : null;

        var access = _tokenService.IssueAccessToken(user, effectiveRoles, leagueId, player?.Id, user.IsSuperAdmin);
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
