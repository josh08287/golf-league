namespace GolfLeague.Domain.Services;

/// <summary>
/// Generates round-robin match-play pairings within a flight using the
/// standard "circle method": fix one player, rotate the rest. Used by
/// GenerateMatchPlayScheduleCommand to build FlightMatch rows.
/// </summary>
public static class RoundRobinScheduler
{
    /// <summary>One pairing within a generated round. Player2Id is null for a bye.</summary>
    public readonly record struct Pairing(int Player1Id, int? Player2Id);

    public enum WeekFitResult
    {
        ExactFit,
        MoreWeeksThanNeeded,
        FewerWeeksThanNeeded,
    }

    /// <summary>
    /// Generates a round-robin schedule via the circle method. For N players,
    /// produces N-1 rounds (even N) each with N/2 pairings, or N rounds (odd
    /// N, via a phantom bye slot) each with (N-1)/2 pairings and one bye.
    /// Returns rounds of pairings in generation order (not yet mapped to
    /// actual weeks) — see <see cref="MapToWeeks"/>.
    /// </summary>
    public static List<List<Pairing>> GenerateCircle(IReadOnlyList<int> playerIds)
    {
        var ids = playerIds.ToList();
        if (ids.Count == 0)
            return [];

        if (ids.Count == 1)
            return [[new Pairing(ids[0], null)]];

        var hasBye = ids.Count % 2 != 0;
        if (hasBye)
            ids.Add(-1); // phantom bye marker

        var n = ids.Count;
        var rounds = new List<List<Pairing>>();
        var fixedPlayer = ids[0];
        var rotating = ids.Skip(1).ToList();

        for (var r = 0; r < n - 1; r++)
        {
            var roundPairings = new List<Pairing>();
            var full = new List<int> { fixedPlayer };
            full.AddRange(rotating);

            for (var i = 0; i < n / 2; i++)
            {
                var a = full[i];
                var b = full[n - 1 - i];
                if (a == -1 || b == -1)
                {
                    var present = a == -1 ? b : a;
                    roundPairings.Add(new Pairing(present, null));
                }
                else
                {
                    roundPairings.Add(new Pairing(a, b));
                }
            }

            rounds.Add(roundPairings);

            // rotate: move last rotating element to front
            var last = rotating[^1];
            rotating.RemoveAt(rotating.Count - 1);
            rotating.Insert(0, last);
        }

        return rounds;
    }

    /// <summary>
    /// Maps generated circle rounds onto the half's actual weekly Round IDs,
    /// in WeekNumber order. If there are more available weeks than circle
    /// rounds, only the earliest weeks get matches — remaining weeks are left
    /// unscheduled for that flight. If there are fewer available weeks than
    /// circle rounds, only as many rounds as there are weeks are scheduled,
    /// in circle order — the schedule is a partial round-robin.
    /// </summary>
    public static (List<(int RoundId, int WeekNumber, List<Pairing> Pairings)> Scheduled, WeekFitResult Fit)
        MapToWeeks(List<List<Pairing>> circleRounds, IReadOnlyList<(int RoundId, int WeekNumber)> availableWeeksAscending)
    {
        var count = Math.Min(circleRounds.Count, availableWeeksAscending.Count);
        var scheduled = new List<(int RoundId, int WeekNumber, List<Pairing> Pairings)>();
        for (var i = 0; i < count; i++)
        {
            var (roundId, weekNumber) = availableWeeksAscending[i];
            scheduled.Add((roundId, weekNumber, circleRounds[i]));
        }

        var fit = circleRounds.Count == availableWeeksAscending.Count
            ? WeekFitResult.ExactFit
            : circleRounds.Count < availableWeeksAscending.Count
                ? WeekFitResult.MoreWeeksThanNeeded
                : WeekFitResult.FewerWeeksThanNeeded;

        return (scheduled, fit);
    }
}
