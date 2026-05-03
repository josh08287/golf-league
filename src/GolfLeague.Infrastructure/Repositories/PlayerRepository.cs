using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _context;

    public PlayerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.Players
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Season)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Players
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Season)
            .Where(p => p.IsActive)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<Player?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken = default)
        => await _context.Players
            .FirstOrDefaultAsync(p => p.EntraObjectId == entraObjectId, cancellationToken);

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default)
    {
        await _context.Players.AddAsync(player, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Player player, CancellationToken cancellationToken = default)
    {
        _context.Players.Update(player);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int playerId, CancellationToken cancellationToken = default)
    {
        var player = await _context.Players.FindAsync([playerId], cancellationToken);
        if (player is not null)
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AssignToFlightAsync(int playerId, int? flightId, CancellationToken cancellationToken = default)
    {
        var activeSeason = await _context.Seasons
            .Where(s => s.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeSeason is null) return;

        var existing = await _context.FlightMemberships
            .Where(fm => fm.PlayerId == playerId && fm.SeasonId == activeSeason.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            _context.FlightMemberships.Remove(existing);

        if (flightId is not null)
        {
            await _context.FlightMemberships.AddAsync(new Domain.Entities.FlightMembership
            {
                PlayerId = playerId,
                FlightId = flightId.Value,
                SeasonId = activeSeason.Id,
                JoinedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
