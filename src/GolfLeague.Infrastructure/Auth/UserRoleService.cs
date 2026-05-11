using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace GolfLeague.Infrastructure.Auth;

public sealed class UserRoleService : IUserRoleService
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "scorer", "player",
    };

    private readonly UserManager<AppUser> _userManager;

    public UserRoleService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<bool>> SetRolesAsync(
        Guid userId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var normalized = roles
            .Select(r => r.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var invalid = normalized.Where(r => !AllowedRoles.Contains(r)).ToList();
        if (invalid.Count > 0)
            return Result<bool>.Fail($"Unknown role(s): {string.Join(", ", invalid)}");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result<bool>.Fail("User not found.");

        var current = (await _userManager.GetRolesAsync(user))
            .Select(r => r.ToLowerInvariant())
            .ToHashSet();

        var toAdd = normalized.Where(r => !current.Contains(r)).ToList();
        var toRemove = current.Where(r => !normalized.Contains(r)).ToList();

        if (toAdd.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                return Result<bool>.Fail($"Failed to add roles: {errors}");
            }
        }
        if (toRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                return Result<bool>.Fail($"Failed to remove roles: {errors}");
            }
        }

        return Result<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Array.Empty<string>();
        return (await _userManager.GetRolesAsync(user))
            .Select(r => r.ToLowerInvariant())
            .ToList();
    }
}
