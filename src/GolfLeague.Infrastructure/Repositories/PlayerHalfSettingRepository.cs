using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class PlayerHalfSettingRepository : IPlayerHalfSettingRepository
{
    private readonly AppDbContext _context;

    public PlayerHalfSettingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PlayerHalfSetting?> GetAsync(int playerId, int halfId, CancellationToken cancellationToken = default)
        => _context.PlayerHalfSettings
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.HalfId == halfId, cancellationToken);

    public async Task<IReadOnlyList<PlayerHalfSetting>> GetForHalfAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.PlayerHalfSettings
            .Where(s => s.HalfId == halfId)
            .ToListAsync(cancellationToken);

    public async Task SetPar3GrossSkinsOptInAsync(int playerId, int halfId, int seasonId, bool optIn, CancellationToken cancellationToken = default)
    {
        // The context defaults to NoTracking, so a plain read-then-mutate on the
        // existing row is never picked up by SaveChangesAsync. Track it explicitly.
        var existing = await _context.PlayerHalfSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.HalfId == halfId, cancellationToken);
        if (existing is null)
        {
            _context.PlayerHalfSettings.Add(new PlayerHalfSetting
            {
                PlayerId = playerId,
                HalfId = halfId,
                SeasonId = seasonId,
                Par3GrossSkinsOptIn = optIn,
            });
        }
        else
        {
            existing.Par3GrossSkinsOptIn = optIn;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }
}
