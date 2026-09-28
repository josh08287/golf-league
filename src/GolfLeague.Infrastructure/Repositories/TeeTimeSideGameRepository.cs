using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class TeeTimeSideGameRepository : ITeeTimeSideGameRepository
{
    private readonly AppDbContext _context;

    public TeeTimeSideGameRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TeeTimeSideGame>> GetForTeeTimeAsync(int teeTimeId, CancellationToken cancellationToken = default)
        => await _context.TeeTimeSideGames
            .Include(g => g.Teams)
            .Include(g => g.HolePicks)
            .Include(g => g.WolfPicks)
            .Where(g => g.TeeTimeId == teeTimeId)
            .ToListAsync(cancellationToken);

    public Task<TeeTimeSideGame?> GetByIdAsync(int sideGameId, CancellationToken cancellationToken = default)
        => _context.TeeTimeSideGames
            .Include(g => g.Teams)
            .Include(g => g.HolePicks)
            .Include(g => g.WolfPicks)
            .FirstOrDefaultAsync(g => g.Id == sideGameId, cancellationToken);

    public async Task<TeeTimeSideGame> AddAsync(TeeTimeSideGame sideGame, CancellationToken cancellationToken = default)
    {
        _context.TeeTimeSideGames.Add(sideGame);
        await _context.SaveChangesAsync(cancellationToken);
        return sideGame;
    }

    public async Task RemoveAsync(int sideGameId, CancellationToken cancellationToken = default)
    {
        await _context.TeeTimeSideGames
            .Where(g => g.Id == sideGameId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<TeeTimeSideGameHolePick> UpsertHolePickAsync(
        int sideGameId, int holeNumber, BbbHonor honor, int? winnerParticipantId, int recordedByPlayerId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.TeeTimeSideGameHolePicks
            .FirstOrDefaultAsync(p => p.TeeTimeSideGameId == sideGameId && p.HoleNumber == holeNumber && p.Honor == honor, cancellationToken);

        if (existing is null)
        {
            existing = new TeeTimeSideGameHolePick
            {
                TeeTimeSideGameId = sideGameId,
                HoleNumber = holeNumber,
                Honor = honor,
            };
            _context.TeeTimeSideGameHolePicks.Add(existing);
        }

        existing.WinnerParticipantId = winnerParticipantId;
        existing.RecordedByPlayerId = recordedByPlayerId;
        existing.RecordedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<TeeTimeWolfHolePick> UpsertWolfPickAsync(
        int sideGameId, int holeNumber, int wolfParticipantId, bool isLoneWolf, bool isBlindWolf, int? partnerParticipantId, int recordedByPlayerId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.TeeTimeWolfHolePicks
            .FirstOrDefaultAsync(p => p.TeeTimeSideGameId == sideGameId && p.HoleNumber == holeNumber, cancellationToken);

        if (existing is null)
        {
            existing = new TeeTimeWolfHolePick
            {
                TeeTimeSideGameId = sideGameId,
                HoleNumber = holeNumber,
            };
            _context.TeeTimeWolfHolePicks.Add(existing);
        }

        existing.WolfParticipantId = wolfParticipantId;
        existing.IsLoneWolf = isLoneWolf;
        existing.IsBlindWolf = isBlindWolf;
        existing.PartnerParticipantId = isLoneWolf ? null : partnerParticipantId;
        existing.RecordedByPlayerId = recordedByPlayerId;
        existing.RecordedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }
}
