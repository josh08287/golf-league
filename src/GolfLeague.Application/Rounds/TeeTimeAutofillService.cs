using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Greedy 2+2 flight pairing as described in the design conversation:
///  - Top off any partial existing tee times first (1- to 3-occupant slots),
///    preferring players from a flight already present in that slot.
///  - For remaining unassigned participants, pull 2 from the largest flight
///    and 2 from the next largest until everyone has a seat. Open new slots
///    in order as needed.
/// </summary>
public sealed class TeeTimeAutofillService : ITeeTimeAutofillService
{
    private readonly ITeeTimeRepository _teeTimes;
    private readonly IRoundRepository _rounds;
    private readonly ILogger<TeeTimeAutofillService> _logger;

    public TeeTimeAutofillService(
        ITeeTimeRepository teeTimes,
        IRoundRepository rounds,
        ILogger<TeeTimeAutofillService> logger)
    {
        _teeTimes = teeTimes;
        _rounds = rounds;
        _logger = logger;
    }

    public async Task<Result<AutofillResult>> RunAsync(int roundId, CancellationToken cancellationToken = default)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return Result<AutofillResult>.Fail($"Round {roundId} not found.");

        // Active participants: not withdrawn, not skipping. These are the
        // people who need a tee time.
        var participants = round.Participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek)
            .ToList();

        var unassigned = participants.Where(p => p.TeeTimeId is null).ToList();
        if (unassigned.Count == 0)
        {
            return Result<AutofillResult>.Ok(new AutofillResult(0, 0));
        }

        // Make sure enough slots exist. Generate slots up to ceil(total / 4).
        var slotsNeeded = TeeTimeSchedule.SlotsNeeded(participants.Count);
        var slots = (await _teeTimes.EnsureSlotsAsync(roundId, slotsNeeded, cancellationToken)).ToList();

        // We need the live occupant lists; re-fetch with includes.
        slots = (await _teeTimes.GetByRoundAsync(roundId, cancellationToken)).ToList();

        var now = DateTime.UtcNow;
        var assignedCount = 0;
        var touchedSlotIds = new HashSet<int>();

        // --- Phase 1: top off partial slots (1-3 occupants). Prefer adding
        // players whose flight is already present in the slot.
        var unassignedQueue = new List<RoundParticipant>(unassigned);
        foreach (var slot in slots.OrderBy(s => s.TeeTimeNumber))
        {
            var occupied = slot.Participants.Count;
            if (occupied == 0 || occupied >= TeeTimeSchedule.CapacityPerTeeTime) continue;

            var seatsLeft = TeeTimeSchedule.CapacityPerTeeTime - occupied;
            var existingFlightIds = slot.Participants.Select(p => p.FlightId).ToHashSet();

            // First pass: same-flight matches.
            var sameFlight = unassignedQueue
                .Where(p => existingFlightIds.Contains(p.FlightId))
                .Take(seatsLeft)
                .ToList();
            foreach (var p in sameFlight)
            {
                await _teeTimes.SetParticipantTeeTimeAsync(p.Id, slot.Id, cancellationToken);
                unassignedQueue.Remove(p);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
                seatsLeft--;
            }
            if (seatsLeft == 0) continue;

            // Second pass: fill remaining seats with the largest available flight.
            while (seatsLeft > 0 && unassignedQueue.Count > 0)
            {
                var donorFlightId = LargestFlight(unassignedQueue);
                var fill = unassignedQueue.First(p => p.FlightId == donorFlightId);
                await _teeTimes.SetParticipantTeeTimeAsync(fill.Id, slot.Id, cancellationToken);
                unassignedQueue.Remove(fill);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
                seatsLeft--;
            }
        }

        // --- Phase 2: assign everyone else to empty slots, greedy 2+2.
        foreach (var slot in slots.Where(s => s.Participants.Count == 0).OrderBy(s => s.TeeTimeNumber))
        {
            if (unassignedQueue.Count == 0) break;

            // Take 2 from the largest flight.
            var picks = TakeFromLargestFlight(unassignedQueue, 2);
            // Take 2 from the next-largest flight.
            picks.AddRange(TakeFromLargestFlight(unassignedQueue, 2));

            foreach (var p in picks)
            {
                await _teeTimes.SetParticipantTeeTimeAsync(p.Id, slot.Id, cancellationToken);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
            }
        }

        foreach (var id in touchedSlotIds)
        {
            await _teeTimes.MarkAutoFilledAsync(id, now, cancellationToken);
        }

        if (unassignedQueue.Count > 0)
        {
            // Shouldn't happen because we sized slots from total participant
            // count, but log it so a future failure mode is visible.
            _logger.LogWarning(
                "Autofill for round {RoundId} left {Count} unassigned participant(s); recompute slot count.",
                roundId, unassignedQueue.Count);
        }

        _logger.LogInformation(
            "Autofill round {RoundId}: assigned {Assigned} player(s) across {Slots} slot(s)",
            roundId, assignedCount, touchedSlotIds.Count);

        return Result<AutofillResult>.Ok(new AutofillResult(assignedCount, touchedSlotIds.Count));
    }

    private static int LargestFlight(List<RoundParticipant> pool)
        => pool.GroupBy(p => p.FlightId)
               .OrderByDescending(g => g.Count())
               .ThenBy(g => g.Key)        // deterministic tie-break
               .First().Key;

    private static List<RoundParticipant> TakeFromLargestFlight(List<RoundParticipant> pool, int max)
    {
        var result = new List<RoundParticipant>();
        if (pool.Count == 0) return result;

        var donorFlightId = LargestFlight(pool);
        var donors = pool.Where(p => p.FlightId == donorFlightId).Take(max).ToList();
        foreach (var d in donors)
        {
            result.Add(d);
            pool.Remove(d);
        }
        return result;
    }
}
