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
            .Include(f => f.Half)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Flights
            .Include(f => f.Season)
            .Include(f => f.Half)
            .Include(f => f.Memberships)
            .OrderBy(f => f.HalfId)
            .ThenBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.Flights
            .Include(f => f.Memberships)
            .Where(f => f.HalfId == halfId)
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

    public async Task AddHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            await _context.SeasonHalves.AddAsync(half, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task UpdateHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default)
    {
        await _context.ExecuteWithBlobSyncAsync(async () =>
        {
            _context.SeasonHalves.Update(half);
            await _context.SaveChangesAsync(cancellationToken);
        }, uploadAfter: true, cancellationToken);
    }

    public async Task<SeasonHalf?> GetHalfByIdAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.SeasonHalves
            .Include(h => h.Flights).ThenInclude(f => f.Memberships)
            .FirstOrDefaultAsync(h => h.Id == halfId, cancellationToken), uploadAfter: false, cancellationToken);

    public async Task<IReadOnlyList<SeasonHalf>> GetHalvesBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.SeasonHalves
            .Where(h => h.SeasonId == seasonId)
            .OrderBy(h => h.HalfNumber)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);

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
            .Include(f => f.Half)
            .Where(f => f.SeasonId == seasonId)
            .OrderBy(f => f.HalfId)
            .ThenBy(f => f.DisplayOrder)
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
        int halfId,
        CancellationToken cancellationToken = default)
        => await _context.ExecuteWithBlobSyncAsync(async () => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.Round)
            .Where(rp =>
                rp.FlightId == flightId &&
                rp.Round.HalfId == halfId &&
                rp.Round.Status == RoundStatus.Finalized &&
                !rp.IsWithdrawn)
            .OrderBy(rp => rp.PlayerId)
            .ToListAsync(cancellationToken), uploadAfter: false, cancellationToken);
}
