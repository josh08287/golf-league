using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Groups a tournament round's participants into tee-time foursomes ordered
/// by ascending handicap (lowest four together, next four together, ...),
/// and into handicap-based "tournament flights" (for the longest-drive
/// award, scoped per flight) matching the flight count of the season's
/// nearest regular half. Tournament rounds don't use player self-service
/// sign-up, standings-based autofill, or season-half flight membership (see
/// TeeTimeService/TeeTimeAutofillService and CreateTournamentRoundCommand's
/// "no flight grouping" comment) — this is the only way their tee times and
/// flights get assigned. Called after the roster changes (creation, add,
/// remove) while the round is still Scheduled.
/// </summary>
public sealed class TournamentFoursomeService
{
    private readonly ITeeTimeRepository _teeTimes;
    private readonly IRoundRepository _rounds;
    private readonly IFlightRepository _flights;

    public TournamentFoursomeService(ITeeTimeRepository teeTimes, IRoundRepository rounds, IFlightRepository flights)
    {
        _teeTimes = teeTimes;
        _rounds = rounds;
        _flights = flights;
    }

    public async Task RegroupAsync(int roundId, IReadOnlyList<RoundParticipant> participants, CancellationToken cancellationToken = default)
    {
        var active = participants.Where(p => !p.IsWithdrawn).ToList();
        var slotsNeeded = TeeTimeSchedule.SlotsNeeded(active.Count);
        var slots = (await _teeTimes.EnsureSlotsAsync(roundId, slotsNeeded, cancellationToken))
            .OrderBy(s => s.TeeTimeNumber)
            .ToList();

        var ordered = active.OrderBy(p => p.HandicapIndex).ThenBy(p => p.PlayerId).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var teeTimeId = slots[i / TeeTimeSchedule.CapacityPerTeeTime].Id;
            if (ordered[i].TeeTimeId != teeTimeId)
                await _teeTimes.SetParticipantTeeTimeAsync(ordered[i].Id, teeTimeId, cancellationToken);
        }

        await RegroupFlightsAsync(roundId, active, cancellationToken);
    }

    /// <summary>
    /// Tee-time grouping driven by the round's current matchups instead of
    /// raw handicap order: consecutive matchup pairs (1&amp;2, 3&amp;4, ...) always
    /// share a tee time, so playing partners land in a matchup they can
    /// watch/verify. Bye matchups (a single player, no opponent) are grouped
    /// the same way among themselves — two byes to a tee time — to use the
    /// fewest slots rather than trailing off at 1-per-slot. Used by
    /// "regenerate from handicaps," which always has a fresh, complete
    /// matchup set to drive from; other callers (creation, roster add/remove)
    /// keep the handicap-ordered <see cref="RegroupAsync"/> above since they
    /// don't guarantee every participant is already in a matchup.
    /// </summary>
    public async Task RegroupTeeTimesFromMatchupsAsync(
        int roundId,
        IReadOnlyList<RoundParticipant> participants,
        IReadOnlyList<TournamentMatchup> matchups,
        CancellationToken cancellationToken = default)
    {
        var byPlayerId = participants
            .Where(p => !p.IsWithdrawn)
            .ToDictionary(p => p.PlayerId);

        var orderedMatchups = matchups.OrderBy(m => m.MatchupNumber).ToList();
        var fullPairs = orderedMatchups.Where(m => m.Player2Id is not null).ToList();
        var byes = orderedMatchups.Where(m => m.Player2Id is null).ToList();

        // Two matchups (4 players) per tee time — capacity is enforced by
        // grouping in twos, not by any capacity check, since a matchup group
        // never exceeds a foursome: 2 full pairs, or up to 2 byes.
        var teeTimeGroups = new List<List<int>>(); // each inner list = player IDs sharing a tee time

        void GroupTwoAtATime(List<TournamentMatchup> group)
        {
            for (var i = 0; i < group.Count; i += 2)
            {
                var playerIds = new List<int> { group[i].Player1Id };
                if (group[i].Player2Id is int p2) playerIds.Add(p2);
                if (i + 1 < group.Count)
                {
                    playerIds.Add(group[i + 1].Player1Id);
                    if (group[i + 1].Player2Id is int p2b) playerIds.Add(p2b);
                }
                teeTimeGroups.Add(playerIds);
            }
        }

        GroupTwoAtATime(fullPairs);
        GroupTwoAtATime(byes);

        var slots = (await _teeTimes.EnsureSlotsAsync(roundId, teeTimeGroups.Count, cancellationToken))
            .OrderBy(s => s.TeeTimeNumber)
            .ToList();

        for (var i = 0; i < teeTimeGroups.Count; i++)
        {
            var teeTimeId = slots[i].Id;
            foreach (var playerId in teeTimeGroups[i])
            {
                if (!byPlayerId.TryGetValue(playerId, out var participant)) continue;
                if (participant.TeeTimeId != teeTimeId)
                    await _teeTimes.SetParticipantTeeTimeAsync(participant.Id, teeTimeId, cancellationToken);
            }
        }

        await RegroupFlightsAsync(roundId, participants.Where(p => !p.IsWithdrawn).ToList(), cancellationToken);
    }

    private async Task RegroupFlightsAsync(int roundId, IReadOnlyList<RoundParticipant> active, CancellationToken cancellationToken)
    {
        var round = await _rounds.GetByIdAsync(roundId, cancellationToken);
        if (round is null) return;

        // Substitutes aren't part of the regular flight grouping — they're
        // often without a season-tracked handicap history, so a handicap-based
        // flight isn't meaningful for them. Leave their TournamentFlightId
        // null; the results page groups them into a separate "Substitutes"
        // bucket appended after the real flights.
        var regulars = active.Where(p => !p.IsSubstitute).ToList();
        foreach (var sub in active.Where(p => p.IsSubstitute))
            await _rounds.SetParticipantTournamentFlightAsync(sub.Id, null, cancellationToken);

        if (regulars.Count == 0) return;

        var flightCount = await ResolveFlightCountAsync(round.SeasonId, round.RoundDate, cancellationToken);
        if (flightCount < 1) flightCount = 1;
        flightCount = Math.Min(flightCount, regulars.Count);

        var newFlights = Enumerable.Range(1, flightCount)
            .Select(n => new TournamentFlight { RoundId = roundId, FlightNumber = n, Name = FlightName(n) })
            .ToList();
        await _rounds.ReplaceTournamentFlightsAsync(roundId, newFlights, cancellationToken);

        var savedFlights = await _rounds.GetTournamentFlightsAsync(roundId, cancellationToken);
        var ordered = regulars.OrderBy(p => p.HandicapIndex).ThenBy(p => p.PlayerId).ToList();
        var perFlight = (int)Math.Ceiling(ordered.Count / (double)flightCount);

        for (var i = 0; i < ordered.Count; i++)
        {
            var flightIndex = Math.Min(i / perFlight, flightCount - 1);
            await _rounds.SetParticipantTournamentFlightAsync(ordered[i].Id, savedFlights[flightIndex].Id, cancellationToken);
        }
    }

    private static string FlightName(int flightNumber) =>
        flightNumber <= 26 ? ((char)('A' + flightNumber - 1)).ToString() : flightNumber.ToString();

    /// <summary>
    /// The flight count to use for a tournament round in <paramref name="seasonId"/>
    /// dated <paramref name="roundDate"/>: the half whose date range contains
    /// the round date, or failing that the half that started most recently
    /// before it. Falls back to 1 if the season has no halves/flights yet.
    /// </summary>
    private async Task<int> ResolveFlightCountAsync(int seasonId, DateOnly roundDate, CancellationToken cancellationToken)
    {
        var halves = await _flights.GetHalvesBySeasonAsync(seasonId, cancellationToken);
        if (halves.Count == 0) return 1;

        var containing = halves.FirstOrDefault(h => roundDate >= h.StartDate && roundDate <= h.EndDate);
        var chosen = containing ?? halves
            .Where(h => h.StartDate <= roundDate)
            .OrderByDescending(h => h.StartDate)
            .FirstOrDefault() ?? halves.OrderByDescending(h => h.StartDate).First();

        var flights = await _flights.GetByHalfAsync(chosen.Id, cancellationToken);
        return flights.Count;
    }
}
