using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
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

    public async Task<IReadOnlyDictionary<Guid, PlayerRole>> GetRolesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, PlayerRole>();

        return await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Role })
            .ToDictionaryAsync(x => x.Id, x => x.Role, cancellationToken);
    }

    public async Task UpdateRoleAsync(Guid userId, PlayerRole role, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return;
        if (user.Role == role) return;
        user.Role = role;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
