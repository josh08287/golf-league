using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Services;

/// <summary>
/// Which side games make sense for a given hole count. Nassau's front/back/
/// overall splits only make sense over a full 18; 2v2 best ball works over
/// either a 9 or an 18. Kept as a static rule table (not a DB field) so
/// adding a new game later is a one-line change here.
/// </summary>
public static class SideGameEligibility
{
    public static readonly IReadOnlyDictionary<SideGameType, int[]> ValidHoleCounts = new Dictionary<SideGameType, int[]>
    {
        [SideGameType.Nassau] = [18],
        [SideGameType.TwoVTwoBestBall] = [9, 18],
        [SideGameType.BingoBangoBongo] = [9, 18],
        [SideGameType.Wolf] = [9, 18],
    };

    public static bool IsValidFor(SideGameType type, int holeCount) =>
        ValidHoleCounts[type].Contains(holeCount);

    public static IReadOnlyList<SideGameType> EligibleGames(int holeCount) =>
        ValidHoleCounts
            .Where(kvp => kvp.Value.Contains(holeCount))
            .Select(kvp => kvp.Key)
            .ToList();
}
