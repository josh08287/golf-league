using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class HandicapRepository : IHandicapRepository
{
    private readonly AppDbContext _context;

    public HandicapRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Handicap?> GetCurrentAsync(int playerId, CancellationToken cancellationToken = default)
        => _context.Handicaps
            .Where(h => h.PlayerId == playerId)
            .OrderByDescending(h => h.EffectiveDate)
            .ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Handicap>> GetHistoryAsync(int playerId, CancellationToken cancellationToken = default)
        => await _context.Handicaps
            .Where(h => h.PlayerId == playerId)
            .OrderByDescending(h => h.EffectiveDate)
            .ThenByDescending(h => h.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Handicap>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Handicaps
            .OrderBy(h => h.PlayerId)
            .ThenByDescending(h => h.EffectiveDate)
            .ThenByDescending(h => h.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<double>> GetLastNNineHoleDifferentialsAsync(
        int playerId,
        int count,
        DateOnly? asOfDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RoundParticipants
            .Include(rp => rp.Round).ThenInclude(r => r.Course)
            .Where(rp =>
                rp.PlayerId == playerId &&
                !rp.IsWithdrawn &&
                !rp.SkippedWeek &&
                !rp.IsSubstitute &&
                rp.TotalGrossStrokes.HasValue &&
                rp.Round.Status == RoundStatus.Finalized);

        if (asOfDate.HasValue)
            query = query.Where(rp => rp.Round.RoundDate <= asOfDate.Value);

        var participants = await query
            .OrderByDescending(rp => rp.Round.RoundDate)
            .ThenByDescending(rp => rp.Id)
            .Take(count)
            .ToListAsync(cancellationToken);

        return participants
            .Where(rp => rp.Round.Course is not null)
            .Select(rp => StablefordScoringService.NineHoleScoreDifferential(
                rp.TotalGrossStrokes!.Value,
                rp.Round.Course.CourseRating,
                rp.Round.Course.SlopeRating))
            .ToList();
    }

    public async Task DeleteCalculatedAsync(int playerId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Handicaps
            .Where(h => h.PlayerId == playerId && h.Source == HandicapSource.Calculated)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            _context.Handicaps.RemoveRange(rows);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteCalculatedForDateAsync(int playerId, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        var rows = await _context.Handicaps
            .Where(h => h.PlayerId == playerId && h.Source == HandicapSource.Calculated && h.EffectiveDate == effectiveDate)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            _context.Handicaps.RemoveRange(rows);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAllCalculatedAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _context.Handicaps
            .Where(h => h.Source == HandicapSource.Calculated)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            _context.Handicaps.RemoveRange(rows);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<int>> GetAllPlayerIdsWithFinalizedRoundsAsync(CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Where(rp =>
                !rp.IsWithdrawn &&
                !rp.SkippedWeek &&
                !rp.IsSubstitute &&
                rp.TotalGrossStrokes.HasValue &&
                rp.Round.Status == RoundStatus.Finalized)
            .Select(rp => rp.PlayerId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DateOnly>> GetFinalizedRoundDatesForPlayerAsync(int playerId, CancellationToken cancellationToken = default)
        => await _context.RoundParticipants
            .Where(rp =>
                rp.PlayerId == playerId &&
                !rp.IsWithdrawn &&
                !rp.SkippedWeek &&
                !rp.IsSubstitute &&
                rp.TotalGrossStrokes.HasValue &&
                rp.Round.Status == RoundStatus.Finalized)
            .Select(rp => rp.Round.RoundDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Handicap handicap, CancellationToken cancellationToken = default)
    {
        await _context.Handicaps.AddAsync(handicap, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
