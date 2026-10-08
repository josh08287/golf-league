using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Services;

/// <summary>
/// Wolf turns follow the order a group actually plays its holes, not hole
/// numbers: the first hole played (a shotgun group's starting hole, otherwise
/// the round's first hole) goes to the first player in the rotation.
/// </summary>
public static class WolfRotation
{
    public static IReadOnlyList<int> PlayOrder(NineHoleSide side, int? startingHoleNumber)
    {
        var holes = side switch
        {
            NineHoleSide.Front => Enumerable.Range(1, 9).ToList(),
            NineHoleSide.Back => Enumerable.Range(10, 9).ToList(),
            _ => Enumerable.Range(1, 18).ToList(),
        };

        var startIdx = startingHoleNumber is int s ? holes.IndexOf(s) : -1;
        if (startIdx <= 0)
            return holes;

        return holes.Skip(startIdx).Concat(holes.Take(startIdx)).ToList();
    }

    /// <summary>Null when the hole isn't part of this round or there's no rotation.</summary>
    public static int? WolfForHole(int holeNumber, IReadOnlyList<int> playOrder, IReadOnlyList<int> rotation)
    {
        var position = playOrder.ToList().IndexOf(holeNumber);
        if (position < 0 || rotation.Count == 0)
            return null;
        return rotation[position % rotation.Count];
    }
}
