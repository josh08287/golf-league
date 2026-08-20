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
            .Include(t => t.Participants).ThenInclude(p => p.Flight).ThenInclude(f => f!.Season)
            .Include(t => t.Participants).ThenInclude(p => p.Flight).ThenInclude(f => f!.Half)
            .Where(t => t.RoundId == roundId)
            .OrderBy(t => t.TeeTimeNumber)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<RoundTeeTime?> GetByIdAsync(int teeTimeId, CancellationToken cancellationToken = default)
        => _context.RoundTeeTimes
            .Include(t => t.Participants).ThenInclude(p => p.Player)
            .Include(t => t.Participants).ThenInclude(p => p.Flight).ThenInclude(f => f!.Season)
            .Include(t => t.Participants).ThenInclude(p => p.Flight).ThenInclude(f => f!.Half)
            .Include(t => t.Participants).ThenInclude(p => p.TournamentFlight)
            .Include(t => t.Participants).ThenInclude(p => p.HoleScores)
            .FirstOrDefaultAsync(t => t.Id == teeTimeId, cancellationToken);

    public async Task<IReadOnlyList<RoundTeeTime>> GetByIdsAsync(IEnumerable<int> teeTimeIds, CancellationToken cancellationToken = default)
    {
        var ids = teeTimeIds.ToList();
        if (ids.Count == 0) return [];

        return await _context.RoundTeeTimes
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

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

    public async Task SwapParticipantTeeTimesAsync(int participantAId, int participantBId, CancellationToken cancellationToken = default)
    {
        var participants = await _context.RoundParticipants
            .AsTracking()
            .Where(p => p.Id == participantAId || p.Id == participantBId)
            .ToListAsync(cancellationToken);

        var a = participants.FirstOrDefault(p => p.Id == participantAId);
        var b = participants.FirstOrDefault(p => p.Id == participantBId);
        if (a is null || b is null) return;

        (a.TeeTimeId, b.TeeTimeId) = (b.TeeTimeId, a.TeeTimeId);
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

    public async Task ApplyAutofillAsync(
        IReadOnlyDictionary<int, int> teeTimeIdByParticipantId,
        IEnumerable<int> touchedSlotIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var participantIds = teeTimeIdByParticipantId.Keys.ToList();
        if (participantIds.Count > 0)
        {
            var participants = await _context.RoundParticipants
                .AsTracking()
                .Where(p => participantIds.Contains(p.Id))
                .ToListAsync(cancellationToken);
            foreach (var participant in participants)
                participant.TeeTimeId = teeTimeIdByParticipantId[participant.Id];
        }

        var slotIds = touchedSlotIds.ToList();
        if (slotIds.Count > 0)
        {
            var slots = await _context.RoundTeeTimes
                .AsTracking()
                .Where(t => slotIds.Contains(t.Id))
                .ToListAsync(cancellationToken);
            foreach (var slot in slots)
                slot.AutoFilledAt = utcNow;
        }

        if (participantIds.Count > 0 || slotIds.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStartingHoleAsync(int teeTimeId, int? startingHoleNumber, CancellationToken cancellationToken = default)
    {
        var slot = await _context.RoundTeeTimes
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == teeTimeId, cancellationToken);
        if (slot is null) return;
        slot.StartingHoleNumber = startingHoleNumber;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
