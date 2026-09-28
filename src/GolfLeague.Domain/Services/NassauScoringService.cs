namespace GolfLeague.Domain.Services;

/// <summary>
/// Nassau match play: separate 1-point matches for the front 9, back 9, and
/// overall 18, each scored hole-by-hole as "up"/"down" with ties pushing.
/// Supports both individual Nassau (every opted-in player has their own 1v1
/// match against every other) and 2v2 team Nassau (each side's best-ball
/// score per hole is compared head-to-head) — both reduce to scoring a
/// single "side A vs side B" match per hole, where a side is one player
/// (individual) or the better of two players' strokes (team).
/// </summary>
public static class NassauScoringService
{
    public enum Leader { Tied, SideA, SideB }

    /// <summary>One hole's result for a single A-vs-B match, already reduced
    /// to a single stroke count per side (the player's own strokes for
    /// individual Nassau, or the team's best-ball strokes for team Nassau).</summary>
    public sealed record HoleResult(int HoleNumber, int SideAStrokes, int SideBStrokes);

    public sealed record SegmentResult(Leader Leader, int HolesUp, int HolesRemaining);

    public sealed record MatchResult(SegmentResult Front, SegmentResult Back, SegmentResult Overall);

    /// <summary>
    /// Reduces a team's per-player hole scores to its best-ball stroke count
    /// for that hole. Used to build <see cref="HoleResult"/> rows for team
    /// Nassau (and shared with 2v2 best ball scoring).
    /// </summary>
    public static int BestBallStrokes(IEnumerable<int> teamPlayerStrokesOnHole) => teamPlayerStrokesOnHole.Min();

    public static SegmentResult ScoreSegment(IReadOnlyList<HoleResult> holes, int totalHolesInSegment)
    {
        int aUp = 0;
        foreach (var hole in holes)
        {
            if (hole.SideAStrokes < hole.SideBStrokes) aUp++;
            else if (hole.SideAStrokes > hole.SideBStrokes) aUp--;
        }

        var leader = aUp switch
        {
            > 0 => Leader.SideA,
            < 0 => Leader.SideB,
            _ => Leader.Tied,
        };

        return new SegmentResult(leader, Math.Abs(aUp), totalHolesInSegment - holes.Count);
    }

    /// <summary>
    /// Scores all three Nassau segments (front 9 = holes 1-9, back 9 = holes
    /// 10-18, overall = all 18) given every hole played so far for one A-vs-B
    /// match.
    /// </summary>
    public static MatchResult Score(IReadOnlyList<HoleResult> allHoles)
    {
        var front = allHoles.Where(h => h.HoleNumber <= 9).ToList();
        var back = allHoles.Where(h => h.HoleNumber >= 10).ToList();

        return new MatchResult(
            ScoreSegment(front, 9),
            ScoreSegment(back, 9),
            ScoreSegment(allHoles, 18));
    }
}
