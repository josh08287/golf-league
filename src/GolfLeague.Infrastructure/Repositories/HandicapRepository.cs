using GolfLeague.Domain.Entities;
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

    public async Task<Handicap?> GetCurrentAsync(int playerId, CancellationToken cancellationToken = default)
        => await _context.Handicaps
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

    public async Task<IReadOnlyList<double>> GetLast20DifferentialsAsync(int playerId, CancellationToken cancellationToken = default)
    {
        var participants = await _context.RoundParticipants
            .Include(rp => rp.Round)
                .ThenInclude(r => r.Course)
            .Where(rp =>
                rp.PlayerId == playerId &&
                !rp.IsWithdrawn &&
                rp.TotalGrossStrokes.HasValue &&
                rp.Round.Status == GolfLeague.Domain.Enums.RoundStatus.Finalized)
            .OrderByDescending(rp => rp.Round.RoundDate)
            .Take(20)
            .ToListAsync(cancellationToken);

        return participants
            .Where(rp => rp.Round.Course is not null)
            .Select(rp => StablefordScoringService.ScoreDifferential(
                rp.TotalGrossStrokes!.Value,
                rp.Round.Course.CourseRating,
                rp.Round.Course.SlopeRating))
            .ToList();
    }

    public async Task AddAsync(Handicap handicap, CancellationToken cancellationToken = default)
    {
        await _context.Handicaps.AddAsync(handicap, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddDifferentialAsync(int playerId, double differential, DateOnly date, CancellationToken cancellationToken = default)
    {
        var handicap = new Handicap
        {
            PlayerId = playerId,
            HandicapIndex = differential,
            EffectiveDate = date,
            Source = GolfLeague.Domain.Enums.HandicapSource.Calculated,
            Notes = "Auto-calculated from 9-hole rounds"
        };
        await _context.Handicaps.AddAsync(handicap, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
