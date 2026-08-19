using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// Groups a tournament round's participants into tee-time foursomes ordered
/// by ascending handicap (lowest four together, next four together, ...).
/// Tournament rounds don't use player self-service sign-up or standings-based
/// autofill (see TeeTimeService/TeeTimeAutofillService) — this is the only
/// way their tee times get assigned. Called after the roster changes
/// (creation, add, remove) while the round is still Scheduled.
/// </summary>
public sealed class TournamentFoursomeService
{
    private readonly ITeeTimeRepository _teeTimes;

    public TournamentFoursomeService(ITeeTimeRepository teeTimes)
    {
        _teeTimes = teeTimes;
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
    }
}
