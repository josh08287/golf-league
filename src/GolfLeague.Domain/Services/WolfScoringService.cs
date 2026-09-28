namespace GolfLeague.Domain.Services;

/// <summary>
/// Wolf: each hole has a rotating Wolf who either picks a partner (2v2
/// best-ball for that hole) or goes it alone against the other 3 ("lone
/// wolf", worth double points). Reuses BestBallScoringService's best-ball
/// primitive for the hole comparison, then applies the point value and
/// splits it across whichever side won.
/// </summary>
public static class WolfScoringService
{
    public const int NormalHolePoints = 1;
    public const int LoneWolfHolePoints = 2;

    public sealed record HolePick(
        int HoleNumber,
        int WolfParticipantId,
        bool IsLoneWolf,
        int? PartnerParticipantId);

    public sealed record HoleOutcome(
        int HoleNumber,
        BestBallScoringService.HoleWinner Winner,
        int PointsAwarded,
        IReadOnlyList<int> WolfSideParticipantIds,
        IReadOnlyList<int> OtherSideParticipantIds);

    public sealed record PlayerTally(int ParticipantId, int Points);

    /// <summary>
    /// Scores one hole given the Wolf's call and every active participant's
    /// net strokes on that hole (keyed by ParticipantId). Participants
    /// missing a score for this hole are excluded from both sides.
    /// </summary>
    public static HoleOutcome ScoreHole(HolePick pick, IReadOnlyDictionary<int, int> netStrokesByParticipant, IReadOnlyList<int> activeParticipantIds)
    {
        var wolfSide = pick.IsLoneWolf
            ? new List<int> { pick.WolfParticipantId }
            : new List<int> { pick.WolfParticipantId, pick.PartnerParticipantId!.Value };

        var otherSide = activeParticipantIds.Except(wolfSide).ToList();

        var wolfStrokes = wolfSide.Where(netStrokesByParticipant.ContainsKey).Select(id => netStrokesByParticipant[id]).ToList();
        var otherStrokes = otherSide.Where(netStrokesByParticipant.ContainsKey).Select(id => netStrokesByParticipant[id]).ToList();

        if (wolfStrokes.Count == 0 || otherStrokes.Count == 0)
            return new HoleOutcome(pick.HoleNumber, BestBallScoringService.HoleWinner.Halved, 0, wolfSide, otherSide);

        var holeResult = BestBallScoringService.ScoreHole(pick.HoleNumber, wolfStrokes, otherStrokes);
        var pointValue = pick.IsLoneWolf ? LoneWolfHolePoints : NormalHolePoints;
        var pointsAwarded = holeResult.Winner == BestBallScoringService.HoleWinner.Halved ? 0 : pointValue;

        return new HoleOutcome(pick.HoleNumber, holeResult.Winner, pointsAwarded, wolfSide, otherSide);
    }

    /// <summary>
    /// Tallies running points per participant across every scored hole. A
    /// side's points are split evenly across its members when it wins (the
    /// lone wolf keeps all points when playing alone and winning).
    /// </summary>
    public static IReadOnlyList<PlayerTally> Tally(IReadOnlyList<HoleOutcome> outcomes)
    {
        var totals = new Dictionary<int, int>();

        foreach (var outcome in outcomes)
        {
            if (outcome.PointsAwarded == 0) continue;

            var winningSide = outcome.Winner == BestBallScoringService.HoleWinner.TeamA
                ? outcome.WolfSideParticipantIds
                : outcome.OtherSideParticipantIds;

            foreach (var participantId in winningSide)
                totals[participantId] = totals.GetValueOrDefault(participantId) + outcome.PointsAwarded;
        }

        return totals
            .Select(kvp => new PlayerTally(kvp.Key, kvp.Value))
            .OrderByDescending(t => t.Points)
            .ToList();
    }
}
