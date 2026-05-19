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

    public Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Players
            .IgnoreQueryFilters()
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Season)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Players
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships).ThenInclude(fm => fm.Season)
            .Where(p => p.IsActive)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

    // AppUser↔Player linkage is 1:1 across all leagues — must be resolvable
    // without a league context (e.g. during invite acceptance with no slug).
    public Task<Player?> GetByAppUserIdAsync(Guid appUserId, CancellationToken cancellationToken = default)
        => _context.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.AppUserId == appUserId, cancellationToken);

    public Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _context.Players.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetUnlinkedActiveAsync(CancellationToken cancellationToken = default)
        => await _context.Players
            .Where(p => p.IsActive && p.AppUserId == null)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

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
        var player = await _context.Players
            .AsTracking()
            .Include(p => p.RoundParticipants).ThenInclude(rp => rp.HoleScores)
            .Include(p => p.FlightMemberships)
            .Include(p => p.Handicaps)
            .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        if (player is null) return;

        // FK cascades from Player are Restrict — wipe child rows explicitly.
        _context.RoundParticipants.RemoveRange(player.RoundParticipants);
        _context.FlightMemberships.RemoveRange(player.FlightMemberships);
        _context.Handicaps.RemoveRange(player.Handicaps);
        _context.Players.Remove(player);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignToFlightAsync(int playerId, int? flightId, CancellationToken cancellationToken = default)
    {
        if (flightId is not null)
        {
            var flight = await _context.Flights
                .FirstOrDefaultAsync(f => f.Id == flightId.Value, cancellationToken);
            if (flight is null) return;

            var existingInHalf = await _context.FlightMemberships
                .AsTracking()
                .FirstOrDefaultAsync(
                    fm => fm.PlayerId == playerId && fm.HalfId == flight.HalfId,
                    cancellationToken);

            if (existingInHalf is not null)
                _context.FlightMemberships.Remove(existingInHalf);

            await _context.FlightMemberships.AddAsync(new FlightMembership
            {
                PlayerId = playerId,
                FlightId = flight.Id,
                SeasonId = flight.SeasonId,
                HalfId = flight.HalfId,
                JoinedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        else
        {
            // Unassign: drop the player's memberships in the active season's halves.
            var activeSeason = await _context.Seasons
                .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);
            if (activeSeason is null) return;

            var existing = await _context.FlightMemberships
                .AsTracking()
                .Where(fm => fm.PlayerId == playerId && fm.SeasonId == activeSeason.Id)
                .ToListAsync(cancellationToken);

            _context.FlightMemberships.RemoveRange(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
