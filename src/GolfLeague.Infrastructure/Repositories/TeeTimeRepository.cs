using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Repositories;

public sealed class TeeTimeRepository : ITeeTimeRepository
{
    private readonly AppDbContext _context;

    public TeeTimeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RoundTeeTime>> GetByRoundAsync(int roundId, CancellationToken cancellationToken = default)
        => await _context.RoundTeeTimes
            .Include(t => t.Participants).ThenInclude(p => p.Player)
            .Include(t => t.Participants).ThenInclude(p => p.Flight)
            .Where(t => t.RoundId == roundId)
            .OrderBy(t => t.TeeTimeNumber)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<RoundTeeTime?> GetByIdAsync(int teeTimeId, CancellationToken cancellationToken = default)
        => _context.RoundTeeTimes
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == teeTimeId, cancellationToken);

    public async Task<IReadOnlyList<RoundTeeTime>> EnsureSlotsAsync(int roundId, int count, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RoundTeeTimes
            .Where(t => t.RoundId == roundId)
            .ToListAsync(cancellationToken);
        var existingByNumber = existing.ToDictionary(t => t.TeeTimeNumber);

        var toAdd = new List<RoundTeeTime>();
        for (var n = 1; n <= count; n++)
        {
            if (existingByNumber.ContainsKey(n)) continue;
            toAdd.Add(new RoundTeeTime
            {
                RoundId = roundId,
                TeeTimeNumber = n,
                ScheduledTime = Domain.Services.TeeTimeSchedule.TimeForSlot(n),
            });
        }

        if (toAdd.Count > 0)
        {
            await _context.RoundTeeTimes.AddRangeAsync(toAdd, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return existing.Concat(toAdd).OrderBy(t => t.TeeTimeNumber).ToList();
    }

    public async Task SetParticipantTeeTimeAsync(int participantId, int? teeTimeId, CancellationToken cancellationToken = default)
    {
        var participant = await _context.RoundParticipants
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == participantId, cancellationToken);
        if (participant is null) return;
        participant.TeeTimeId = teeTimeId;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAutoFilledAsync(int teeTimeId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var slot = await _context.RoundTeeTimes
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == teeTimeId, cancellationToken);
        if (slot is null) return;
        slot.AutoFilledAt = utcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
