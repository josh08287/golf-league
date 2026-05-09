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
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Seasons
            .Include(s => s.Halves)
            .OrderByDescending(s => s.Year)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(
            async () => await _context.Seasons
                .Include(s => s.Halves)
                .FirstOrDefaultAsync(s => s.IsActive, cancellationToken),
            uploadAfter: false,
            cancellationToken);

    public async Task AddAsync(Season season, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            await _context.Seasons.AddAsync(season, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task SetActiveAsync(int seasonId, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            await _context.Seasons
                .Where(s => s.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);

            await _context.Seasons
                .Where(s => s.Id == seasonId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(
            async () => await _context.Seasons
                .Include(s => s.Halves)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken),
            uploadAfter: false,
            cancellationToken);

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            var season = await _context.Seasons.FindAsync([id], cancellationToken);
            if (season is not null)
            {
                _context.Seasons.Remove(season);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }, uploadAfter: true, cancellationToken);
    }
}
