using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Greedy 2+2 flight pairing:
///  - On the final round of a half: group participants by standings rank within
///    their flight (1+2 together, 3+4 together, …), pairing adjacent-ranked
///    players from different flights per slot. Time preferences are ignored.
///  - All other rounds: top off any partial existing tee times first, then
///    assign remaining participants with 2+2 flight pairing across the largest
///    remaining flights. Time preferences are a soft weight when ordering
///    candidates for a slot, never a guarantee of (or bar to) any slot.
/// </summary>
public sealed class TeeTimeAutofillService : ITeeTimeAutofillService
{
    private readonly ITeeTimeRepository _teeTimes;
    private readonly IRoundRepository _rounds;
    private readonly IFlightRepository _flights;
    private readonly ILogger<TeeTimeAutofillService> _logger;

    public TeeTimeAutofillService(
        ITeeTimeRepository teeTimes,
        IRoundRepository rounds,
        IFlightRepository flights,
        ILogger<TeeTimeAutofillService> logger)
    {
        _teeTimes = teeTimes;
        _rounds = rounds;
        _flights = flights;
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

        // SetParticipantTeeTimeAsync writes straight to the DB and never
        // updates the in-memory slot.Participants collections fetched above,
        // so track this run's assignments ourselves. Effective occupancy =
        // what was loaded + what we've added; without it a later phase sees
        // a stale count of 0 and overfills a slot past capacity.
        var addedThisRun = new Dictionary<int, int>();
        int EffectiveCount(RoundTeeTime slot)
            => slot.Participants.Count + addedThisRun.GetValueOrDefault(slot.Id);
        void NoteAssigned(int slotId)
            => addedThisRun[slotId] = addedThisRun.GetValueOrDefault(slotId) + 1;

        // --- Phase 0 (final round of half only): standings-based pairing.
        // On the last scheduled round of the half, ignore time preferences and
        // group players so that adjacent standings rivals share a tee time:
        // rank 1+2 together, rank 3+4 together, etc. Players without a flight
        // or standings data fall through to the normal phases below.
        if (round.HalfId.HasValue && await IsLastRoundOfHalfAsync(round, cancellationToken))
        {
            var unassignedForStandings = participants.Where(p => p.TeeTimeId is null).ToList();
            var standingsBuckets = await BuildStandingsBucketsAsync(
                round.HalfId.Value, unassignedForStandings, cancellationToken);

            // Interleave one bucket-pair from each flight into each slot.
            var emptySlotQueue = slots
                .Where(s => s.Participants.Count == 0)
                .OrderBy(s => s.TeeTimeNumber)
                .ToList();

            var placedInPhase0 = new HashSet<int>();
            int slotIndex = 0;
            while (standingsBuckets.Any(b => b.Count > 0) && slotIndex < emptySlotQueue.Count)
            {
                var slot = emptySlotQueue[slotIndex++];
                var picks = new List<RoundParticipant>();

                // Take up to 2 from each flight bucket in order until the slot
                // is full (max 4 players).
                foreach (var bucket in standingsBuckets)
                {
                    if (picks.Count >= TeeTimeSchedule.CapacityPerTeeTime) break;
                    var take = Math.Min(2, TeeTimeSchedule.CapacityPerTeeTime - picks.Count);
                    picks.AddRange(bucket.Take(take));
                    bucket.RemoveRange(0, Math.Min(take, bucket.Count));
                }

                foreach (var p in picks)
                {
                    await _teeTimes.SetParticipantTeeTimeAsync(p.Id, slot.Id, cancellationToken);
                    assignedCount++;
                    touchedSlotIds.Add(slot.Id);
                    placedInPhase0.Add(p.Id);
                    NoteAssigned(slot.Id);
                }
            }

            unassigned.RemoveAll(p => placedInPhase0.Contains(p.Id));
        }

        // --- Phase 1: top off partial slots (1-3 occupants). Prefer adding
        // players whose flight is already present in the slot.
        var unassignedQueue = new List<RoundParticipant>(unassigned);
        foreach (var slot in slots.OrderBy(s => s.TeeTimeNumber))
        {
            var occupied = EffectiveCount(slot);
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
                NoteAssigned(slot.Id);
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
                NoteAssigned(slot.Id);
                seatsLeft--;
            }
        }

        // --- Phase 2: assign everyone else to empty slots, greedy 2+2.
        // Tee-time preferences act as a WEIGHT, not a guarantee: when picking
        // from the donor flight, players who prefer this slot's band come
        // first, players with no preference next, and players preferring a
        // different band last — but anyone may be seated anywhere when the
        // remaining seats demand it.
        var emptySlots = slots
            .Where(s => EffectiveCount(s) == 0)
            .OrderBy(s => s.TeeTimeNumber)
            .ToList();

        var totalSlots = slots.Count;

        foreach (var slot in emptySlots)
        {
            if (unassignedQueue.Count == 0) break;

            var seatsLeft = TeeTimeSchedule.CapacityPerTeeTime - EffectiveCount(slot);
            if (seatsLeft <= 0) continue;

            var bandFlag = SlotBand(slot.TeeTimeNumber, totalSlots);

            var picks = TakeFromLargestFlight(unassignedQueue, Math.Min(2, seatsLeft), bandFlag);
            if (picks.Count < seatsLeft)
                picks.AddRange(TakeFromLargestFlight(unassignedQueue, seatsLeft - picks.Count, bandFlag));

            foreach (var p in picks)
            {
                await _teeTimes.SetParticipantTeeTimeAsync(p.Id, slot.Id, cancellationToken);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
                NoteAssigned(slot.Id);
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

    /// <summary>
    /// Returns which third (Early/Middle/Late) the given 1-based slot number
    /// falls into for a round with <paramref name="totalSlots"/> slots.
    /// </summary>
    private static TeeTimeSlotPreference SlotBand(int teeTimeNumber, int totalSlots)
    {
        if (totalSlots <= 0) return TeeTimeSlotPreference.Early;
        var third = Math.Max(1, totalSlots / 3);
        if (teeTimeNumber <= third) return TeeTimeSlotPreference.Early;
        if (teeTimeNumber <= third * 2) return TeeTimeSlotPreference.Middle;
        return TeeTimeSlotPreference.Late;
    }

    private static int? LargestFlight(List<RoundParticipant> pool)
        => pool.GroupBy(p => p.FlightId)
               .OrderByDescending(g => g.Count())
               .ThenBy(g => g.Key)        // deterministic tie-break
               .First().Key;

    /// <summary>
    /// Weight used to order candidates for a slot: prefer players whose
    /// preference includes the slot's band, then players with no preference,
    /// and defer players who prefer a different band — without ever
    /// excluding anyone.
    /// </summary>
    private static int PreferenceWeight(TeeTimeSlotPreference preference, TeeTimeSlotPreference bandFlag)
    {
        if (preference == TeeTimeSlotPreference.None) return 0;
        return (preference & bandFlag) != 0 ? 1 : -1;
    }

    private static List<RoundParticipant> TakeFromLargestFlight(
        List<RoundParticipant> pool, int max, TeeTimeSlotPreference bandFlag)
    {
        var result = new List<RoundParticipant>();
        if (pool.Count == 0) return result;

        var donorFlightId = LargestFlight(pool);
        var donors = pool
            .Where(p => p.FlightId == donorFlightId)
            .OrderByDescending(p => PreferenceWeight(p.Player.PreferredTeeTimeSlots, bandFlag))
            .ThenBy(p => p.PlayerId)       // deterministic tie-break
            .Take(max)
            .ToList();
        foreach (var d in donors)
        {
            result.Add(d);
            pool.Remove(d);
        }
        return result;
    }

    /// <summary>
    /// Returns true if <paramref name="round"/> is the last scheduled (non-cancelled)
    /// round in its half by WeekNumber.
    /// </summary>
    private async Task<bool> IsLastRoundOfHalfAsync(Round round, CancellationToken cancellationToken)
    {
        if (!round.HalfId.HasValue) return false;
        var halfRounds = await _rounds.GetByHalfAsync(round.HalfId.Value, cancellationToken);
        var maxWeek = halfRounds
            .Where(r => r.Status != RoundStatus.Cancelled)
            .Select(r => r.WeekNumber)
            .DefaultIfEmpty(0)
            .Max();
        return round.WeekNumber == maxWeek;
    }

    /// <summary>
    /// For each flight represented in <paramref name="unassigned"/>, queries half
    /// standings and returns a list of ranked participant buckets — one list per
    /// flight, ordered by standing rank (best first). Players with no standings
    /// data are appended at the end of their flight's bucket.
    /// </summary>
    private async Task<List<List<RoundParticipant>>> BuildStandingsBucketsAsync(
        int halfId,
        List<RoundParticipant> unassigned,
        CancellationToken cancellationToken)
    {
        var flightIds = unassigned
            .Where(p => p.FlightId.HasValue)
            .Select(p => p.FlightId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var buckets = new List<List<RoundParticipant>>();

        foreach (var flightId in flightIds)
        {
            var flightParticipants = unassigned.Where(p => p.FlightId == flightId).ToList();
            var standingsRows = await _flights.GetStandingsAsync(flightId, halfId, cancellationToken);

            // Aggregate total net points per player from finalized rounds this half.
            var pointsByPlayer = standingsRows
                .GroupBy(rp => rp.PlayerId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(rp => rp.TotalNetStablefordPoints ?? 0));

            // Rank: most points first, then by PlayerId for determinism.
            var ranked = flightParticipants
                .OrderByDescending(p => pointsByPlayer.GetValueOrDefault(p.PlayerId, 0))
                .ThenBy(p => p.PlayerId)
                .ToList();

            buckets.Add(ranked);
        }

        // Players with no flight assignment fall into a single leftover bucket.
        var noFlight = unassigned.Where(p => !p.FlightId.HasValue).ToList();
        if (noFlight.Count > 0)
            buckets.Add(noFlight);

        return buckets;
    }
}
