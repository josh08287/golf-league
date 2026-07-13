using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class AppUserRepository : IAppUserRepository
{
    private readonly AppDbContext _context;

    public AppUserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.ToUpperInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return [];

        return await _context.Users.Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRolesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<string>>();

        // Walk UserRoles join + Roles dict in one query, group by user, project to a dict.
        var rows = await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles,
                  ur => ur.RoleId,
                  r => r.Id,
                  (ur, r) => new { ur.UserId, RoleName = r.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => (x.RoleName ?? string.Empty).ToLowerInvariant())
                    .Where(s => s.Length > 0)
                    .ToList());
    }

    public async Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
