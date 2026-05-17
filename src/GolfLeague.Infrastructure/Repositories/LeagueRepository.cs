using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class LeagueRepository : ILeagueRepository
{
    private readonly AppDbContext _context;

    public LeagueRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<League?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => _context.Leagues.FirstOrDefaultAsync(l => l.Slug == slug, cancellationToken);

    public Task<League?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Leagues.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<League>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Leagues
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

    public Task<LeagueMembership?> GetMembershipAsync(int leagueId, Guid userId, CancellationToken cancellationToken = default)
        => _context.LeagueMemberships
            .FirstOrDefaultAsync(m => m.LeagueId == leagueId && m.UserId == userId, cancellationToken);

    public async Task AddAsync(League league, CancellationToken cancellationToken = default)
    {
        await _context.Leagues.AddAsync(league, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(League league, CancellationToken cancellationToken = default)
    {
        _context.Leagues.Update(league);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
