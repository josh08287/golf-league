namespace GolfLeague.Domain.Services;

/// <summary>
/// Wolf: each hole has a rotating Wolf who either picks a partner (2v2
/// best-ball for that hole), goes it alone after watching the other 3 tee
/// off ("lone wolf"), or declares alone before anyone has hit ("blind
/// wolf" — the boldest, highest-stakes call). Reuses
/// BestBallScoringService's best-ball primitive for the hole comparison,
/// then applies whichever point rule matches the call:
///   - Partnered: win = 1 point per player on the winning side.
///   - Lone wolf: win = 2 points to the Wolf alone; loss = the other 3
///     split 2 points (points are pooled per side, same shape as partnered).
///   - Blind wolf: win = 4 points to the Wolf alone; loss = 1 point EACH to
///     the other 3 (3 total, not pooled — this is the asymmetric case that
///     makes blind wolf a bigger gamble than a plain lone wolf).
/// A halved hole (tied best ball) never awards points, regardless of call.
/// </summary>
public static class WolfScoringService
{
    public const int NormalHolePoints = 1;
    public const int LoneWolfHolePoints = 2;
    public const int BlindWolfWinPoints = 4;
    public const int BlindWolfLossPointsPerOpponent = 1;

    public sealed record HolePick(
        int HoleNumber,
        int WolfParticipantId,
        bool IsLoneWolf,
        bool IsBlindWolf,
        int? PartnerParticipantId);

    public sealed record HoleOutcome(
        int HoleNumber,
        BestBallScoringService.HoleWinner Winner,
        IReadOnlyList<int> WolfSideParticipantIds,
        IReadOnlyList<int> OtherSideParticipantIds,
        /// <summary>Points awarded to whichever side actually won the hole (0 if halved).</summary>
        int PointsAwarded,
        bool IsBlindWolf);

    public sealed record PlayerTally(int ParticipantId, int Points);

    /// <summary>
    /// Scores one hole given the Wolf's call and every active participant's
    /// net strokes on that hole (keyed by ParticipantId). Participants
    /// missing a score for this hole are excluded from both sides. Blind
    /// wolf implies lone wolf (going in blind means no partner either way).
    /// </summary>
    public static HoleOutcome ScoreHole(HolePick pick, IReadOnlyDictionary<int, int> netStrokesByParticipant, IReadOnlyList<int> activeParticipantIds)
    {
        var isAlone = pick.IsLoneWolf || pick.IsBlindWolf;
        var wolfSide = isAlone
            ? new List<int> { pick.WolfParticipantId }
            : new List<int> { pick.WolfParticipantId, pick.PartnerParticipantId!.Value };

        var otherSide = activeParticipantIds.Except(wolfSide).ToList();

        var wolfStrokes = wolfSide.Where(netStrokesByParticipant.ContainsKey).Select(id => netStrokesByParticipant[id]).ToList();
        var otherStrokes = otherSide.Where(netStrokesByParticipant.ContainsKey).Select(id => netStrokesByParticipant[id]).ToList();

        if (wolfStrokes.Count == 0 || otherStrokes.Count == 0)
            return new HoleOutcome(pick.HoleNumber, BestBallScoringService.HoleWinner.Halved, wolfSide, otherSide, 0, pick.IsBlindWolf);

        var holeResult = BestBallScoringService.ScoreHole(pick.HoleNumber, wolfStrokes, otherStrokes);
        if (holeResult.Winner == BestBallScoringService.HoleWinner.Halved)
            return new HoleOutcome(pick.HoleNumber, holeResult.Winner, wolfSide, otherSide, 0, pick.IsBlindWolf);

        var wolfWon = holeResult.Winner == BestBallScoringService.HoleWinner.TeamA;
        int pointsAwarded;
        if (pick.IsBlindWolf)
            pointsAwarded = wolfWon ? BlindWolfWinPoints : BlindWolfLossPointsPerOpponent * otherSide.Count;
        else
            pointsAwarded = isAlone ? LoneWolfHolePoints : NormalHolePoints;

        return new HoleOutcome(pick.HoleNumber, holeResult.Winner, wolfSide, otherSide, pointsAwarded, pick.IsBlindWolf);
    }

    /// <summary>
    /// Tallies running points per participant across every scored hole.
    /// Normal/lone-wolf wins pool the hole's points evenly across the
    /// winning side (the lone wolf keeps all of it alone). Blind wolf is the
    /// one asymmetric case: a Wolf win awards all 4 points to the Wolf only,
    /// while a Wolf loss awards 1 point to each of the 3 opponents (not
    /// pooled/split).
    /// </summary>
    public static IReadOnlyList<PlayerTally> Tally(IReadOnlyList<HoleOutcome> outcomes)
    {
        var totals = new Dictionary<int, int>();

        void Add(int participantId, int points) =>
            totals[participantId] = totals.GetValueOrDefault(participantId) + points;

        foreach (var outcome in outcomes)
        {
            if (outcome.PointsAwarded == 0) continue;

            var wolfWon = outcome.Winner == BestBallScoringService.HoleWinner.TeamA;

            if (outcome.IsBlindWolf)
            {
                if (wolfWon)
                    Add(outcome.WolfSideParticipantIds[0], outcome.PointsAwarded);
                else
                    foreach (var participantId in outcome.OtherSideParticipantIds)
                        Add(participantId, BlindWolfLossPointsPerOpponent);
            }
            else
            {
                var winningSide = wolfWon ? outcome.WolfSideParticipantIds : outcome.OtherSideParticipantIds;
                foreach (var participantId in winningSide)
                    Add(participantId, outcome.PointsAwarded);
            }
        }

        return totals
            .Select(kvp => new PlayerTally(kvp.Key, kvp.Value))
            .OrderByDescending(t => t.Points)
            .ToList();
    }
}
