using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class InviteRepository : IInviteRepository
{
    private readonly AppDbContext _context;

    public InviteRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PlayerInvite?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.PlayerInvites.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    // Token is a cryptographically random secret — it is self-authorizing and must
    // be resolvable without a league context (e.g. invite emails contain no slug).
    public Task<PlayerInvite?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => _context.PlayerInvites.IgnoreQueryFilters().Include(i => i.League).FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

    public async Task<IReadOnlyList<PlayerInvite>> GetByStatusAsync(InviteStatus status, CancellationToken cancellationToken = default)
        => await _context.PlayerInvites
            .Where(i => i.Status == status)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerInvite>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.PlayerInvites
            .Include(i => i.League)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> PendingInviteExistsForEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.PlayerInvites
            .AnyAsync(
                i => i.Email == email.ToLower() && i.Status == InviteStatus.Pending && i.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

    // Email-based invite lookup is used during social auth where no league context
    // is available. Email is validated against the authenticated social profile.
    public Task<PlayerInvite?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.PlayerInvites
            .IgnoreQueryFilters()
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(
                i => i.Email == email.ToLower() && i.Status == InviteStatus.Pending && i.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

    public async Task AddAsync(PlayerInvite invite, CancellationToken cancellationToken = default)
    {
        await _context.PlayerInvites.AddAsync(invite, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<PlayerInvite> invites, CancellationToken cancellationToken = default)
    {
        await _context.PlayerInvites.AddRangeAsync(invites, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlayerInvite invite, CancellationToken cancellationToken = default)
    {
        _context.PlayerInvites.Update(invite);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PlayerInvite invite, CancellationToken cancellationToken = default)
    {
        _context.PlayerInvites.Remove(invite);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
