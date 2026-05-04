using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class FlightRepository : IFlightRepository
{
    private readonly AppDbContext _context;

    public FlightRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Flight?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Flights
            .Include(f => f.Season)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Flights
            .Include(f => f.Season)
            .Include(f => f.Memberships)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task AddAsync(Flight flight, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            await _context.Flights.AddAsync(flight, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task DeleteAsync(int flightId, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            var flight = await _context.Flights.FindAsync([flightId], cancellationToken);
            if (flight is not null)
            {
                _context.Flights.Remove(flight);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }, uploadAfter: true, cancellationToken);
    }

    public async Task<int?> GetActiveSeasonIdAsync(CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Seasons
            .Where(s => s.IsActive)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Flights
            .Where(f => f.SeasonId == seasonId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<FlightMembership>> GetMembershipsAsync(
        int flightId,
        CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.FlightMemberships
            .Where(fm => fm.FlightId == flightId)
            .Include(fm => fm.Player)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<RoundParticipant>> GetStandingsAsync(
        int flightId,
        int seasonId,
        CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.Round)
            .Where(rp =>
                rp.Round.FlightId == flightId &&
                rp.Round.SeasonId == seasonId &&
                rp.Round.Status == RoundStatus.Finalized &&
                !rp.IsWithdrawn)
            .OrderBy(rp => rp.PlayerId)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);
}
