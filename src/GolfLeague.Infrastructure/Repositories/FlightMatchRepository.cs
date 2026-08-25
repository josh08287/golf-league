using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class FlightMatchRepository : IFlightMatchRepository
{
    private readonly AppDbContext _context;

    public FlightMatchRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<FlightMatch>> GetByHalfAsync(int halfId, CancellationToken cancellationToken = default)
        => await _context.FlightMatches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Include(m => m.Round)
            .Where(m => m.HalfId == halfId)
            .OrderBy(m => m.WeekNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FlightMatch>> GetByFlightAsync(int flightId, int halfId, CancellationToken cancellationToken = default)
        => await _context.FlightMatches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Include(m => m.Round)
            .Where(m => m.FlightId == flightId && m.HalfId == halfId)
            .OrderBy(m => m.WeekNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FlightMatch>> GetByRoundAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.FlightMatches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Where(m => m.RoundId == roundId)
            .ToListAsync(cancellationToken);

    public Task<FlightMatch?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => _context.FlightMatches
            .Include(m => m.Player1)
            .Include(m => m.Player2)
            .Include(m => m.Round)
            .Include(m => m.HoleResults)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<FlightMatch> matches, CancellationToken cancellationToken = default)
    {
        await _context.FlightMatches.AddRangeAsync(matches, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByHalfAsync(int halfId, CancellationToken cancellationToken = default)
    {
        await _context.FlightMatches
            .Where(m => m.HalfId == halfId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task ReplaceHoleResultsAsync(int flightMatchId, IEnumerable<FlightMatchHoleResult> results, CancellationToken cancellationToken = default)
    {
        await _context.FlightMatchHoleResults
            .Where(r => r.FlightMatchId == flightMatchId)
            .ExecuteDeleteAsync(cancellationToken);

        await _context.FlightMatchHoleResults.AddRangeAsync(results, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateMatchTotalsAsync(FlightMatch match, CancellationToken cancellationToken = default)
    {
        _context.FlightMatches.Update(match);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
