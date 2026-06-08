using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class LeagueSettingRepository : ILeagueSettingRepository
{
    private readonly AppDbContext _context;

    public LeagueSettingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LeagueSetting>> GetAllAsync(int leagueId, CancellationToken cancellationToken = default)
        => await _context.LeagueSettings
            .Where(s => s.LeagueId == leagueId)
            .ToListAsync(cancellationToken);

    public Task<LeagueSetting?> GetAsync(int leagueId, string key, CancellationToken cancellationToken = default)
        => _context.LeagueSettings
            .FirstOrDefaultAsync(s => s.LeagueId == leagueId && s.Key == key, cancellationToken);

    public async Task UpsertAsync(int leagueId, string key, string value, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(leagueId, key, cancellationToken);
        if (existing is null)
        {
            _context.LeagueSettings.Add(new LeagueSetting { LeagueId = leagueId, Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }
}
