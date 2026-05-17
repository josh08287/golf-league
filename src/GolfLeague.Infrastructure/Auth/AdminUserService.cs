using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Auth;

public sealed class AdminUserService : IAdminUserService
{
    private const string AdminRole = "admin";

    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IUserRoleService _roleService;
    private readonly IPlayerRepository _playerRepository;
    private readonly IHandicapRepository _handicapRepository;
    private readonly ILeagueContext _leagueContext;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        UserManager<AppUser> userManager,
        AppDbContext dbContext,
        IUserRoleService roleService,
        IPlayerRepository playerRepository,
        IHandicapRepository handicapRepository,
        ILeagueContext leagueContext,
        ILogger<AdminUserService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _roleService = roleService;
        _playerRepository = playerRepository;
        _handicapRepository = handicapRepository;
        _leagueContext = leagueContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AdminUserDto>> ListAdminOnlyUsersAsync(
        CancellationToken cancellationToken = default)
    {
        // "Admin-only" = no linked Player row. Some of these will be admins
        // proper, others might be users who signed up via invite but haven't
        // had a Player created yet (rare given how invites auto-create
        // Players, but still possible).
        var users = await _dbContext.Users
            .Where(u => u.PlayerId == null)
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        if (users.Count == 0) return Array.Empty<AdminUserDto>();

        var userIds = users.Select(u => u.Id).ToList();

        // Bulk-fetch roles, passkey presence, and lockout state.
        var roleRows = await _dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_dbContext.Roles,
                  ur => ur.RoleId,
                  r => r.Id,
                  (ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => (x.RoleName ?? string.Empty).ToLowerInvariant())
                    .Where(s => s.Length > 0)
                    .ToList());

        var passkeyUserIds = await _dbContext.UserPasskeys
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var passkeySet = passkeyUserIds.ToHashSet();

        var now = DateTimeOffset.UtcNow;

        return users.Select(u => new AdminUserDto(
            Id: u.Id,
            Email: u.Email ?? string.Empty,
            Roles: rolesByUser.TryGetValue(u.Id, out var rs) ? rs : Array.Empty<string>(),
            CreatedAt: u.CreatedAt,
            LastLoginAt: u.LastLoginAt,
            HasPassword: !string.IsNullOrEmpty(u.PasswordHash),
            HasTotp: u.TotpEnabled,
            HasPasskey: passkeySet.Contains(u.Id),
            IsLockedOut: u.LockoutEnd.HasValue && u.LockoutEnd.Value > now))
            .ToList();
    }

    public async Task<Result<AdminUserDto>> SetRolesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result<AdminUserDto>.Fail("User not found.");

        var normalized = roles.Select(r => r.Trim().ToLowerInvariant()).ToHashSet();

        // Last-admin guard: if this user currently holds admin and the new
        // set doesn't, make sure someone else still has it.
        var wasAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
        var willBeAdmin = normalized.Contains(AdminRole);
        if (wasAdmin && !willBeAdmin && !await AnotherAdminExistsAsync(user.Id, cancellationToken))
            return Result<AdminUserDto>.Fail("Cannot remove the last admin.");

        var setResult = await _roleService.SetRolesAsync(userId, roles, cancellationToken);
        if (!setResult.IsSuccess) return Result<AdminUserDto>.Fail(setResult.Error!);

        var dto = await BuildSingleAsync(user, cancellationToken);
        return Result<AdminUserDto>.Ok(dto);
    }

    public async Task<Result<bool>> ResetMfaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return Result<bool>.Fail("User not found.");

        user.TotpSecret = null;
        user.TotpEnabled = false;

        var passkeys = await _dbContext.UserPasskeys
            .AsTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        _dbContext.UserPasskeys.RemoveRange(passkeys);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Admin reset MFA for user {UserId}; {Passkeys} passkey(s) removed", userId, passkeys.Count);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result<bool>.Fail("User not found.");

        if (await _userManager.IsInRoleAsync(user, AdminRole)
            && !await AnotherAdminExistsAsync(user.Id, cancellationToken))
        {
            return Result<bool>.Fail("Cannot delete the last admin.");
        }

        // FK cascades from AspNetUsers handle role assignments, external
        // logins, claims, tokens. RefreshTokens and UserPasskeys are configured
        // Cascade in the model, so they go too.
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result<bool>.Fail($"Failed to delete user: {errors}");
        }

        _logger.LogWarning("Admin deleted user {UserId} ({Email})", userId, user.Email);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<PlayerDto>> AttachPlayerProfileAsync(
        Guid userId,
        string firstName,
        string lastName,
        double initialHandicap,
        int? flightId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Result<PlayerDto>.Fail("First and last name are required.");

        if (_leagueContext.LeagueId is null)
            return Result<PlayerDto>.Fail("No league context.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result<PlayerDto>.Fail("User not found.");

        if (user.PlayerId is not null)
            return Result<PlayerDto>.Fail("This user already has a player profile.");

        if (string.IsNullOrWhiteSpace(user.Email))
            return Result<PlayerDto>.Fail("User has no email address; cannot create a player profile.");

        // Try to adopt an existing unlinked Player with the same email. Only
        // do this lookup when the user has an email (which we already required
        // above, so this is unconditional here).
        var existing = await _playerRepository.GetByEmailAsync(user.Email, cancellationToken);
        Player player;
        if (existing is not null && existing.AppUserId is null)
        {
            existing.AppUserId = user.Id;
            existing.FirstName = firstName;
            existing.LastName = lastName;
            // Player.Email matched user.Email by construction, but force it
            // explicitly in case the row was created without an email or with
            // a stale one.
            existing.Email = user.Email;
            await _playerRepository.UpdateAsync(existing, cancellationToken);
            player = existing;
            _logger.LogInformation(
                "Adopted existing Player {PlayerId} for AppUser {UserId} by email match",
                player.Id, user.Id);
        }
        else if (existing is not null)
        {
            // Email matches a Player already linked to a different AppUser.
            return Result<PlayerDto>.Fail(
                "A player with this email is already linked to a different account.");
        }
        else
        {
            player = new Player
            {
                LeagueId = _leagueContext.LeagueId.Value,
                FirstName = firstName,
                LastName = lastName,
                Email = user.Email,
                IsActive = true,
                AppUserId = user.Id,
            };
            await _playerRepository.AddAsync(player, cancellationToken);
            await _handicapRepository.AddAsync(new Handicap
            {
                PlayerId = player.Id,
                HandicapIndex = initialHandicap,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = HandicapSource.Initial,
            }, cancellationToken);
            _logger.LogInformation("Created Player {PlayerId} for AppUser {UserId}", player.Id, user.Id);
        }

        user.PlayerId = player.Id;
        await _userManager.UpdateAsync(user);

        if (flightId is int fId)
        {
            try
            {
                await _playerRepository.AssignToFlightAsync(player.Id, fId, cancellationToken);
            }
            catch (Exception ex)
            {
                // Player is linked; surface flight failure but don't roll back.
                _logger.LogWarning(ex, "Flight assignment failed for player {PlayerId}", player.Id);
                return Result<PlayerDto>.Fail(
                    $"Player linked but flight assignment failed: {ex.Message}");
            }
        }

        // Re-read current handicap (existing players may have history we kept).
        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var roles = (await _userManager.GetRolesAsync(user))
            .Select(r => r.ToLowerInvariant())
            .ToList();

        var dto = new PlayerDto(
            player.Id,
            player.FullName,
            player.Email,
            player.IsActive,
            currentHandicap?.HandicapIndex ?? initialHandicap,
            null,
            null,
            roles,
            user.Id);

        return Result<PlayerDto>.Ok(dto);
    }

    public async Task<Result<PlayerDto>> LinkPlayerToUserAsync(
        int playerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var player = await _playerRepository.GetByIdAsync(playerId, cancellationToken);
        if (player is null) return Result<PlayerDto>.Fail("Player not found.");
        if (player.AppUserId is not null)
            return Result<PlayerDto>.Fail("Player is already linked to a user account.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result<PlayerDto>.Fail("User not found.");
        if (user.PlayerId is not null)
            return Result<PlayerDto>.Fail("User is already linked to a player profile.");

        // Set both sides of the 1:1 link in a single transaction-ish sequence.
        // Player.Email is overwritten with AppUser.Email so future email-based
        // operations (invite, password reset, etc.) stay consistent.
        player.AppUserId = user.Id;
        if (!string.IsNullOrWhiteSpace(user.Email))
            player.Email = user.Email;
        await _playerRepository.UpdateAsync(player, cancellationToken);

        user.PlayerId = player.Id;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation(
            "Manually linked Player {PlayerId} to AppUser {UserId}",
            player.Id, user.Id);

        var currentHandicap = await _handicapRepository.GetCurrentAsync(player.Id, cancellationToken);
        var roles = (await _userManager.GetRolesAsync(user))
            .Select(r => r.ToLowerInvariant())
            .ToList();

        var dto = new PlayerDto(
            player.Id,
            player.FullName,
            player.Email,
            player.IsActive,
            currentHandicap?.HandicapIndex,
            null,
            null,
            roles,
            user.Id);

        return Result<PlayerDto>.Ok(dto);
    }

    private async Task<bool> AnotherAdminExistsAsync(Guid excludingUserId, CancellationToken cancellationToken)
    {
        var adminRoleId = await _dbContext.Roles
            .Where(r => r.Name == AdminRole)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (adminRoleId is null) return false;

        return await _dbContext.UserRoles
            .AnyAsync(ur => ur.RoleId == adminRoleId.Value && ur.UserId != excludingUserId, cancellationToken);
    }

    private async Task<AdminUserDto> BuildSingleAsync(AppUser user, CancellationToken cancellationToken)
    {
        var roles = (await _userManager.GetRolesAsync(user))
            .Select(r => r.ToLowerInvariant())
            .ToList();
        var hasPasskey = await _dbContext.UserPasskeys
            .AnyAsync(p => p.UserId == user.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return new AdminUserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            Roles: roles,
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt,
            HasPassword: await _userManager.HasPasswordAsync(user),
            HasTotp: user.TotpEnabled,
            HasPasskey: hasPasskey,
            IsLockedOut: user.LockoutEnd.HasValue && user.LockoutEnd.Value > now);
    }
}
