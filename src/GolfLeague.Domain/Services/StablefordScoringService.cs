using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Services;

public static class StablefordScoringService
{
    public static int CourseHandicap(double handicapIndex, int slopeRating, RoundType roundType = RoundType.EighteenHole)
    {
        var fullCourseHandicap = (int)Math.Round(handicapIndex * slopeRating / 113.0, MidpointRounding.AwayFromZero);
        return roundType == RoundType.NineHole ? (int)Math.Round(fullCourseHandicap / 2.0, MidpointRounding.AwayFromZero) : fullCourseHandicap;
    }

    public static int StrokesOnHole(int courseHandicap, int strokeIndex, RoundType roundType = RoundType.EighteenHole)
    {
        var divisor = roundType == RoundType.NineHole ? 9 : 18;
        return (int)Math.Floor(courseHandicap / (double)divisor) + (strokeIndex <= courseHandicap % divisor ? 1 : 0);
    }

    /// <summary>
    /// Calculates strokes on a hole for 9-hole rounds, normalizing stroke index by ranking within the nine.
    /// </summary>
    /// <param name="courseHandicap">The 9-hole course handicap</param>
    /// <param name="strokeIndex">The hole's 18-hole stroke index</param>
    /// <param name="allStrokeIndicesInNine">All stroke indices for the 9-hole side being played</param>
    /// <returns>Number of handicap strokes to apply on this hole</returns>
    public static int StrokesOnHole(int courseHandicap, int strokeIndex, IReadOnlyList<int> allStrokeIndicesInNine)
    {
        // Normalize: rank this hole's stroke index among all holes in this nine (1 = hardest, 9 = easiest)
        var sortedIndices = allStrokeIndicesInNine.OrderBy(si => si).ToList();
        var normalizedIndex = sortedIndices.IndexOf(strokeIndex) + 1; // Convert to 1-based rank
        return (int)Math.Floor(courseHandicap / 9.0) + (normalizedIndex <= courseHandicap % 9 ? 1 : 0);
    }

    public static int NetStrokes(int grossStrokes, int strokesOnHole)
        => grossStrokes - strokesOnHole;

    public static int StablefordPoints(int par, int netStrokes)
        => Math.Clamp(par + 2 - netStrokes, 0, 6);

    public static bool IsNetDoubleBogey(int grossStrokes, int par, int strokesOnHole)
        => grossStrokes >= MaxGross(par, strokesOnHole);

    public static int MaxGross(int par, int strokesOnHole)
        => par + 2 + strokesOnHole;

    public static double ScoreDifferential(int grossStrokes, double courseRating, int slopeRating)
        => (grossStrokes - courseRating) * 113.0 / slopeRating;

    public static double NineHoleScoreDifferential(int grossStrokes, double courseRating, int slopeRating)
        => (grossStrokes - (courseRating / 2)) * (113.0 / slopeRating);
}
