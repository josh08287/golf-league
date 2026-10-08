using GolfLeague.Domain.Entities;

namespace GolfLeague.Application.Rounds;

/// <summary>
/// A participant's round totals are only meaningful for a complete round —
/// standings, rankings, and handicaps all read them that way — so they're set
/// from the hole scores once every hole has one, and cleared otherwise.
/// </summary>
public static class RoundTotals
{
    public static bool Apply(RoundParticipant participant, IReadOnlyCollection<HoleScore> holeScores, int expectedHoleCount)
    {
        var isComplete = expectedHoleCount > 0 && holeScores.Select(h => h.HoleNumber).Distinct().Count() == expectedHoleCount;

        participant.TotalGrossStrokes = isComplete ? holeScores.Sum(h => h.GrossStrokes) : null;
        participant.TotalNetStrokes = isComplete ? holeScores.Sum(h => h.NetStrokes) : null;
        participant.TotalGrossStablefordPoints = isComplete ? holeScores.Sum(h => h.GrossStablefordPoints) : null;
        participant.TotalNetStablefordPoints = isComplete ? holeScores.Sum(h => h.NetStablefordPoints) : null;

        return isComplete;
    }
}
