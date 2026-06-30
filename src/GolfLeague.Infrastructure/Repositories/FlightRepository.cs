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

    public Task<Flight?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.Flights
            .Include(f => f.Season)
            .Include(f => f.Half)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Flights
            .Include(f => f.Season)
            .Include(f => f.Half)
            .Include(f => f.Memberships)
            .OrderBy(f => f.HalfId)
            .ThenBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.Flights
            .Include(f => f.Season)
            .Include(f => f.Half)
            .Include(f => f.Memberships)
            .Where(f => f.HalfId == halfId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Flight flight, CancellationToken cancellationToken = default)
    {
        await _context.Flights.AddAsync(flight, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default)
    {
        await _context.SeasonHalves.AddAsync(half, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateHalfAsync(SeasonHalf half, CancellationToken cancellationToken = default)
    {
        _context.SeasonHalves.Update(half);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<SeasonHalf?> GetHalfByIdAsync(int halfId, CancellationToken cancellationToken = default)
        => _context.SeasonHalves
            .Include(h => h.Season)
            .Include(h => h.Flights).ThenInclude(f => f.Memberships)
            .FirstOrDefaultAsync(h => h.Id == halfId, cancellationToken);

    public async Task<IReadOnlyList<SeasonHalf>> GetHalvesBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.SeasonHalves
            .Where(h => h.SeasonId == seasonId)
            .OrderBy(h => h.HalfNumber)
            .ToListAsync(cancellationToken);

    public async Task DeleteAsync(int flightId, CancellationToken cancellationToken = default)
    {
        await _context.Flights
            .Where(f => f.Id == flightId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int?> GetActiveSeasonIdAsync(CancellationToken cancellationToken = default)
        => await _context.Seasons
            .Where(s => s.IsActive)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.Flights
            .Include(f => f.Season)
            .Include(f => f.Half)
            .Where(f => f.SeasonId == seasonId)
            .OrderBy(f => f.HalfId)
            .ThenBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FlightMembership>> GetMembershipsAsync(
        int flightId,
        CancellationToken cancellationToken = default)
        => await _context.FlightMemberships
            .Where(fm => fm.FlightId == flightId)
            .Include(fm => fm.Player)
            .ToListAsync(cancellationToken);

    public async Task AddMembershipAsync(FlightMembership membership, CancellationToken cancellationToken = default)
    {
        await _context.FlightMemberships.AddAsync(membership, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsHalfLockedAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.Rounds.AnyAsync(
            r => r.HalfId == halfId &&
                 (r.Status == RoundStatus.InProgress ||
                  r.Status == RoundStatus.PendingFinalization ||
                  r.Status == RoundStatus.Finalized),
            cancellationToken);

    public async Task<IReadOnlyList<RoundParticipant>> GetStandingsAsync(
        int flightId,
        int halfId,
        CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.Round)
            .Where(rp =>
                rp.FlightId == flightId &&
                rp.Round.HalfId == halfId &&
                rp.Round.Status == RoundStatus.Finalized &&
                !rp.IsWithdrawn)
            .OrderBy(rp => rp.PlayerId)
            .ToListAsync(cancellationToken);
}
