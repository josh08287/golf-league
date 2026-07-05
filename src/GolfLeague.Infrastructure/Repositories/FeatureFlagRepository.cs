using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class FeatureFlagRepository : IFeatureFlagRepository
{
    private readonly AppDbContext _context;

    public FeatureFlagRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.FeatureFlags.ToListAsync(cancellationToken);

    public Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
        => _context.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, cancellationToken);

    public async Task UpsertAsync(string key, bool enabled, CancellationToken cancellationToken = default)
    {
        // The context defaults to NoTracking, so a plain read-then-mutate on the
        // existing row is never picked up by SaveChangesAsync. Track it explicitly.
        var existing = await _context.FeatureFlags
            .AsTracking()
            .FirstOrDefaultAsync(f => f.Key == key, cancellationToken);
        if (existing is null)
        {
            _context.FeatureFlags.Add(new FeatureFlag { Key = key, Enabled = enabled });
        }
        else
        {
            existing.Enabled = enabled;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }
}
