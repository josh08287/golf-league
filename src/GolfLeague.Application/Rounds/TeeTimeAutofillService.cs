using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Fills tee times front to back (earliest slot first), always finishing a
/// slot to a full foursome before moving to the next:
///  - On the final round of a half: group participants by standings rank within
///    their flight (1+2 together, 3+4 together, …), pairing adjacent-ranked
///    players from different flights per slot. Time preferences are ignored.
///  - All other rounds: top off any partial existing tee times first, then
///    assign remaining participants slot by slot, seat by seat. Same-flight
///    grouping (2+2) is a soft preference, not a hard rule — it's relaxed
///    whenever needed to complete full foursomes (e.g. flights of 3/3/2 must
///    still pack into two full foursomes rather than stranding a lone
///    player). Time preferences are a soft weight when ordering candidates
///    for a slot, never a guarantee of (or bar to) any slot.
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
        // Participants this run placed into each slot, by slot id — used by
        // the twosome rebalance below, which may only move a player autofill
        // itself just seated, never a pre-existing manual pick.
        var placedThisRun = new Dictionary<int, List<RoundParticipant>>();
        int EffectiveCount(RoundTeeTime slot)
            => slot.Participants.Count + addedThisRun.GetValueOrDefault(slot.Id);
        void NoteAssigned(int slotId, RoundParticipant participant)
        {
            addedThisRun[slotId] = addedThisRun.GetValueOrDefault(slotId) + 1;
            if (!placedThisRun.TryGetValue(slotId, out var list))
                placedThisRun[slotId] = list = new List<RoundParticipant>();
            list.Add(participant);
        }

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
                    NoteAssigned(slot.Id, p);
                }
            }

            unassigned.RemoveAll(p => placedInPhase0.Contains(p.Id));
        }

        // --- Phase 1: top off partial slots (1-3 occupants). Prefer adding
        // players whose flight is already present in the slot. Time
        // preferences are a soft weight here too — same as Phase 2 — so a
        // player who asked for an early slot isn't shoved into topping off a
        // partial late slot just because a same-flight seat is open there.
        var unassignedQueue = new List<RoundParticipant>(unassigned);
        var totalSlots = slots.Count;
        foreach (var slot in slots.OrderBy(s => s.TeeTimeNumber))
        {
            var occupied = EffectiveCount(slot);
            if (occupied == 0 || occupied >= TeeTimeSchedule.CapacityPerTeeTime) continue;

            var seatsLeft = TeeTimeSchedule.CapacityPerTeeTime - occupied;
            var existingFlightIds = slot.Participants.Select(p => p.FlightId).ToHashSet();
            var slotBand = SlotBand(slot.TeeTimeNumber, totalSlots);

            // First pass: same-flight matches, best preference match first.
            var sameFlight = unassignedQueue
                .Where(p => existingFlightIds.Contains(p.FlightId))
                .OrderByDescending(p => PreferenceWeight(p.Player.PreferredTeeTimeSlots, slotBand))
                .ThenBy(p => p.PlayerId)
                .Take(seatsLeft)
                .ToList();
            foreach (var p in sameFlight)
            {
                await _teeTimes.SetParticipantTeeTimeAsync(p.Id, slot.Id, cancellationToken);
                unassignedQueue.Remove(p);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
                NoteAssigned(slot.Id, p);
                seatsLeft--;
            }
            if (seatsLeft == 0) continue;

            // Second pass: fill remaining seats from the largest available
            // flight, preferring the best preference match within that flight.
            while (seatsLeft > 0 && unassignedQueue.Count > 0)
            {
                var donorFlightId = LargestFlight(unassignedQueue);
                var fill = unassignedQueue
                    .Where(p => p.FlightId == donorFlightId)
                    .OrderByDescending(p => PreferenceWeight(p.Player.PreferredTeeTimeSlots, slotBand))
                    .ThenBy(p => p.PlayerId)
                    .First();
                await _teeTimes.SetParticipantTeeTimeAsync(fill.Id, slot.Id, cancellationToken);
                unassignedQueue.Remove(fill);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
                NoteAssigned(slot.Id, fill);
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

        foreach (var slot in emptySlots)
        {
            if (unassignedQueue.Count == 0) break;

            var seatsLeft = TeeTimeSchedule.CapacityPerTeeTime - EffectiveCount(slot);
            if (seatsLeft <= 0) continue;

            var bandFlag = SlotBand(slot.TeeTimeNumber, totalSlots);
            var flightCounts = new Dictionary<int, int>();

            // Fill one seat at a time so a full foursome is never blocked by
            // fragmented flight remainders (e.g. flights of 3/3/2 must still
            // pack into two full foursomes). Prefer a flight already in this
            // slot with fewer than 2 picks so far (soft "2 from the same
            // flight" grouping), then fall back to the largest remaining
            // flight overall, so seats are always filled when players remain.
            while (seatsLeft > 0 && unassignedQueue.Count > 0)
            {
                var pick = TakeOneForSlot(unassignedQueue, flightCounts, bandFlag);
                await _teeTimes.SetParticipantTeeTimeAsync(pick.Id, slot.Id, cancellationToken);
                assignedCount++;
                touchedSlotIds.Add(slot.Id);
                NoteAssigned(slot.Id, pick);
                var flightKey = pick.FlightId ?? NoFlightKey;
                flightCounts[flightKey] = flightCounts.GetValueOrDefault(flightKey) + 1;
                seatsLeft--;
            }
        }

        // --- Phase 3: avoid a trailing twosome. ceil(n/4) slots can leave
        // exactly one slot with only 2 occupants (n mod 4 == 2). Rather than
        // publish a twosome, borrow one player from a full foursome slot to
        // make two threesomes. The threesomes don't have to be adjacent —
        // any full slot will do — but only a player autofill placed THIS RUN
        // may be moved; pre-existing (manual or prior-run) occupants are
        // never touched, per the rule that manual picks are permanent. Among
        // candidate donor slots, prefer one whose this-run placements include
        // a mover that doesn't prefer the donor slot's band — a player
        // deliberately matched there by Phase 1/2's preference weighting
        // shouldn't be bumped out just because they happened to be added
        // first, when a worse-fit mover is available in another full slot.
        foreach (var slot in slots)
        {
            if (EffectiveCount(slot) != 2) continue;

            var donorCandidates = slots
                .Where(s => s.Id != slot.Id
                         && EffectiveCount(s) == TeeTimeSchedule.CapacityPerTeeTime
                         && placedThisRun.GetValueOrDefault(s.Id)?.Count > 0)
                .Select(s =>
                {
                    var band = SlotBand(s.TeeTimeNumber, totalSlots);
                    var mover = placedThisRun[s.Id]
                        .OrderBy(p => PreferenceWeight(p.Player.PreferredTeeTimeSlots, band))
                        .First();
                    return (Slot: s, Mover: mover, MoverWeight: PreferenceWeight(mover.Player.PreferredTeeTimeSlots, band));
                })
                .OrderBy(x => x.MoverWeight)
                .ToList();
            if (donorCandidates.Count == 0) continue;

            var (donorSlot, mover, _) = donorCandidates[0];
            await _teeTimes.SetParticipantTeeTimeAsync(mover.Id, slot.Id, cancellationToken);

            placedThisRun[donorSlot.Id].Remove(mover);
            addedThisRun[donorSlot.Id]--;
            NoteAssigned(slot.Id, mover);
            touchedSlotIds.Add(slot.Id);
            touchedSlotIds.Add(donorSlot.Id);
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
    /// Number of tee times per Early/Middle band when there are enough slots
    /// to use fixed-size bands (see <see cref="SlotBand"/>).
    /// </summary>
    private const int FixedBandSize = 3;

    /// <summary>
    /// Returns which band (Early/Middle/Late) the given 1-based slot number
    /// falls into for a round with <paramref name="totalSlots"/> slots.
    /// With 9 or more slots, Early = tee times 1-3, Middle = 4-6, and Late =
    /// 7 through the end (absorbing any overflow beyond 9). With fewer than
    /// 9 slots there aren't enough tee times for that fixed split, so the
    /// slots are divided into three bands as evenly as proportional thirds
    /// allow.
    /// </summary>
    private static TeeTimeSlotPreference SlotBand(int teeTimeNumber, int totalSlots)
    {
        if (totalSlots <= 0) return TeeTimeSlotPreference.Early;

        if (totalSlots >= FixedBandSize * 3)
        {
            if (teeTimeNumber <= FixedBandSize) return TeeTimeSlotPreference.Early;
            if (teeTimeNumber <= FixedBandSize * 2) return TeeTimeSlotPreference.Middle;
            return TeeTimeSlotPreference.Late;
        }

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

    /// <summary>Sentinel dictionary key standing in for a null FlightId.</summary>
    private const int NoFlightKey = -1;

    /// <summary>
    /// Picks a single participant to seat next in a slot that's being built
    /// up from empty. Prefers a flight already represented in this slot with
    /// fewer than 2 picks so far (so foursomes lean 2+2 by flight when
    /// possible), otherwise falls back to whichever remaining flight is
    /// largest — guaranteeing every seat gets filled even when flight sizes
    /// don't split evenly into pairs (e.g. flights of 3/3/2 must still pack
    /// into full foursomes rather than stranding a lone player).
    /// </summary>
    private static RoundParticipant TakeOneForSlot(
        List<RoundParticipant> pool,
        Dictionary<int, int> flightCountsInSlot,
        TeeTimeSlotPreference bandFlag)
    {
        int? donorFlightId = null;
        var foundFlightAlreadyInSlot = false;
        foreach (var (flightKey, count) in flightCountsInSlot)
        {
            var flightId = flightKey == NoFlightKey ? (int?)null : flightKey;
            if (count >= 2 || !pool.Any(p => p.FlightId == flightId)) continue;
            donorFlightId = flightId;
            foundFlightAlreadyInSlot = true;
            break;
        }
        if (!foundFlightAlreadyInSlot)
            donorFlightId = LargestFlight(pool);

        var pick = pool
            .Where(p => p.FlightId == donorFlightId)
            .OrderByDescending(p => PreferenceWeight(p.Player.PreferredTeeTimeSlots, bandFlag))
            .ThenBy(p => p.PlayerId)       // deterministic tie-break
            .First();

        pool.Remove(pick);
        return pick;
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
