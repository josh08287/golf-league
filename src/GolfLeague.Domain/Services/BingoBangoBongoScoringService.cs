using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Services;

/// <summary>
/// Bingo Bango Bongo: 3 individual honors per hole (first on the green,
/// closest to the pin once everyone is on, first in the hole), each worth 1
/// point. A hole's honor with no recorded winner (tie, or nobody clearly
/// first) awards no point to anyone — points are never split.
/// </summary>
public static class BingoBangoBongoScoringService
{
    public sealed record HolePick(int HoleNumber, BbbHonor Honor, int? WinnerParticipantId);

    public sealed record PlayerTally(int ParticipantId, int Points);

    /// <summary>
    /// Tallies total points per participant from every recorded pick.
    /// Participants with zero points are omitted — callers merge with the
    /// full roster if a 0 needs to be shown.
    /// </summary>
    public static IReadOnlyList<PlayerTally> Tally(IReadOnlyList<HolePick> picks) =>
        picks
            .Where(p => p.WinnerParticipantId.HasValue)
            .GroupBy(p => p.WinnerParticipantId!.Value)
            .Select(g => new PlayerTally(g.Key, g.Count()))
            .OrderByDescending(t => t.Points)
            .ToList();
}
