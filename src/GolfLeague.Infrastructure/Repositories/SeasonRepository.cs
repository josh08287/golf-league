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
            .Include(s => s.Halves)
            .OrderByDescending(s => s.Year)
            .ToListAsync(cancellationToken);

    public Task<Season?> GetActiveAsync(CancellationToken cancellationToken = default)
        => _context.Seasons
            .Include(s => s.Halves)
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

    public async Task AddAsync(Season season, CancellationToken cancellationToken = default)
    {
        await _context.Seasons.AddAsync(season, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(int seasonId, CancellationToken cancellationToken = default)
    {
        // Two ExecuteUpdates in a single transaction: clear the active flag on
        // any other season, then set it on the target. Wrapped in the
        // execution strategy so transient retries replay the whole transaction.
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.Seasons
                .Where(s => s.IsActive && s.Id != seasonId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);

            await _context.Seasons
                .Where(s => s.Id == seasonId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), cancellationToken);

            await tx.CommitAsync(cancellationToken);
        });
    }

    public Task<Season?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Seasons
            .Include(s => s.Halves)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _context.Seasons
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
