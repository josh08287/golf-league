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
        => await _context.Flights
            .Include(f => f.Season)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Flight>> GetBySeasonAsync(int seasonId, CancellationToken cancellationToken = default)
        => await _context.Flights
            .Where(f => f.SeasonId == seasonId)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RoundParticipant>> GetStandingsAsync(
        int flightId,
        int seasonId,
        CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Include(rp => rp.Player)
            .Include(rp => rp.Round)
            .Where(rp =>
                rp.Round.FlightId == flightId &&
                rp.Round.SeasonId == seasonId &&
                rp.Round.Status == RoundStatus.Finalized &&
                !rp.IsWithdrawn)
            .OrderBy(rp => rp.PlayerId)
            .ToListAsync(cancellationToken);
}
