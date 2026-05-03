using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class SeasonRepository : ISeasonRepository
{
    private readonly AppDbContext _context;

    public SeasonRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Seasons
            .OrderByDescending(s => s.Year)
            .ToListAsync(cancellationToken);

    public async Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

    public async Task AddAsync(Season season, CancellationToken cancellationToken = default)
    {
        await _context.Seasons.AddAsync(season, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int seasonId, CancellationToken cancellationToken = default)
    {
        await _context.Seasons
            .Where(s => s.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);

        await _context.Seasons
            .Where(s => s.Id == seasonId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.Seasons.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var season = await _context.Seasons.FindAsync([id], cancellationToken);
        if (season is not null)
        {
            _context.Seasons.Remove(season);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
