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

    public async Task<IReadOnlyList<double>> GetLast20NineHoleDifferentialsAsync(int playerId, CancellationToken cancellationToken = default)
    {
        var participants = await _context.RoundParticipants
            .Include(rp => rp.Round).ThenInclude(r => r.Course)
            .Where(rp =>
                rp.PlayerId == playerId &&
                !rp.IsWithdrawn &&
                !rp.SkippedWeek &&
                rp.TotalGrossStrokes.HasValue &&
                rp.Round.Status == RoundStatus.Finalized)
            .OrderByDescending(rp => rp.Round.RoundDate)
            .ThenByDescending(rp => rp.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        return participants
            .Where(rp => rp.Round.Course is not null)
            .Select(rp => StablefordScoringService.NineHoleScoreDifferential(
                rp.TotalGrossStrokes!.Value,
                rp.Round.Course.CourseRating,
                rp.Round.Course.SlopeRating))
            .ToList();
    }

    public async Task<double?> GetLowIndexInLast365DaysAsync(int playerId, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var cutoff = asOf.AddDays(-365);
        var values = await _context.Handicaps
            .Where(h => h.PlayerId == playerId && h.EffectiveDate >= cutoff && h.EffectiveDate <= asOf)
            .Select(h => h.HandicapIndex)
            .ToListAsync(cancellationToken);

        if (values.Count == 0) return null;
        return values.Min();
    }

    public async Task AddAsync(Handicap handicap, CancellationToken cancellationToken = default)
    {
        await _context.Handicaps.AddAsync(handicap, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
