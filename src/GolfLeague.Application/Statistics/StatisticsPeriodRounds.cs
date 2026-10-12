using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;

namespace GolfLeague.Application.Statistics;

/// <summary>
/// Resolves the rounds a statistics period covers: a half, a season, or all time.
///
/// Tournament rounds belong to a season but never to a half (HalfId is null),
/// so a plain half lookup would drop them — and with them any course that has
/// only hosted tournaments. For a half, the season's tournaments dated within
/// the half's start/end dates are included alongside the half's own rounds.
/// </summary>
public static class StatisticsPeriodRounds
{
    public static async Task<IReadOnlyList<Round>> GetAsync(
        IRoundRepository roundRepository,
        IFlightRepository flightRepository,
        int? seasonId,
        int? halfId,
        CancellationToken cancellationToken)
    {
        if (halfId is int hId)
        {
            var halfRounds = await roundRepository.GetByHalfAsync(hId, cancellationToken);
            var half = await flightRepository.GetHalfByIdAsync(hId, cancellationToken);
            if (half is null)
                return halfRounds;

            var tournaments = (await roundRepository.GetBySeasonAsync(half.SeasonId, cancellationToken))
                .Where(r => r.RoundType == RoundType.Tournament
                    && r.HalfId is null
                    && r.RoundDate >= half.StartDate
                    && r.RoundDate <= half.EndDate);

            return halfRounds.Concat(tournaments).ToList();
        }

        return seasonId is int sId
            ? await roundRepository.GetBySeasonAsync(sId, cancellationToken)
            : await roundRepository.GetAllAsync(cancellationToken);
    }
}
