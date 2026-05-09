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
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Players
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Season)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Players
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Flight)
            .Include(p => p.FlightMemberships)
                .ThenInclude(fm => fm.Season)
            .Where(p => p.IsActive)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<Player?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(
            async () => await _context.Players.FirstOrDefaultAsync(p => p.EntraObjectId == entraObjectId, cancellationToken),
            uploadAfter: false,
            cancellationToken);

    public async Task AddAsync(Player player, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            await _context.Players.AddAsync(player, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task UpdateAsync(Player player, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            _context.Players.Update(player);
            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task DeleteAsync(int playerId, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            var player = await _context.Players
                .Include(p => p.RoundParticipants)
                    .ThenInclude(rp => rp.HoleScores)
                .FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
            if (player is not null)
            {
                _context.RoundParticipants.RemoveRange(player.RoundParticipants);
                _context.Players.Remove(player);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }, uploadAfter: true, cancellationToken);
    }

    public async Task AssignToFlightAsync(int playerId, int? flightId, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            // A flight is scoped to a half (post-refactor), so we resolve the
            // half from the chosen flight and replace only this player's
            // membership in that same half. Membership in the other half is
            // preserved.
            if (flightId is not null)
            {
                var flight = await _context.Flights
                    .FirstOrDefaultAsync(f => f.Id == flightId.Value, cancellationToken);
                if (flight is null) return;

                var existingInHalf = await _context.FlightMemberships
                    .Where(fm => fm.PlayerId == playerId && fm.HalfId == flight.HalfId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingInHalf is not null)
                    _context.FlightMemberships.Remove(existingInHalf);

                await _context.FlightMemberships.AddAsync(new Domain.Entities.FlightMembership
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
                // Unassign: drop the player's membership in the active season's
                // halves. (The drag-to-Unassigned column hits this branch.)
                var activeSeason = await _context.Seasons
                    .Where(s => s.IsActive)
                    .FirstOrDefaultAsync(cancellationToken);
                if (activeSeason is null) return;

                var existing = await _context.FlightMemberships
                    .Where(fm => fm.PlayerId == playerId && fm.SeasonId == activeSeason.Id)
                    .ToListAsync(cancellationToken);

                _context.FlightMemberships.RemoveRange(existing);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }
}
