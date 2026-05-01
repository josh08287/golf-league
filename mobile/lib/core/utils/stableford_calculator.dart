import 'dart:math' as math;

/// Pure utility class for Stableford scoring calculations.
/// No state, all methods are static.
class StablefordCalculator {
  StablefordCalculator._();

  /// Calculates the course handicap from a handicap index and slope rating.
  ///
  /// Formula: ROUND(handicapIndex × (slopeRating / 113))
  static int courseHandicap(double handicapIndex, int slopeRating) {
    return (handicapIndex * slopeRating / 113).round();
  }

  /// Returns how many handicap strokes a player receives on a specific hole.
  ///
  /// Formula:
  ///   FLOOR(courseHandicap / 18) + (1 IF strokeIndex <= courseHandicap MOD 18 ELSE 0)
  ///
  /// Special case: if courseHandicap MOD 18 == 0 the extra stroke is never awarded,
  /// meaning a player with exactly 18 or 36 strokes gets exactly 1 or 2 on every hole.
  static int strokesOnHole(int courseHandicap, int strokeIndex) {
    final base = courseHandicap ~/ 18;
    final remainder = courseHandicap % 18;
    final extra = (remainder > 0 && strokeIndex <= remainder) ? 1 : 0;
    return base + extra;
  }

  /// Returns the net strokes for a hole after subtracting handicap strokes.
  static int netStrokes(int grossStrokes, int strokesReceived) {
    return grossStrokes - strokesReceived;
  }

  /// Returns Stableford points for a net score vs par.
  ///
  /// | Net vs Par | Points |
  /// |------------|--------|
  /// | +2 or worse| 0      |
  /// | +1 (bogey) | 1      |
  /// |  0 (par)   | 2      |
  /// | -1 (birdie)| 3      |
  /// | -2 (eagle) | 4      |
  /// | -3 (alb.)  | 5      |
  /// | -4 or better| 6     |
  static int points(int netStrokes, int par) {
    final diff = netStrokes - par;
    if (diff <= -4) return 6;
    if (diff == -3) return 5;
    if (diff == -2) return 4;
    if (diff == -1) return 3;
    if (diff == 0) return 2;
    if (diff == 1) return 1;
    return 0;
  }

  /// Returns true if [grossStrokes] equals or exceeds the net double bogey cap.
  ///
  /// Max gross score per hole: Par + 2 + handicapStrokesOnHole
  static bool isNetDoubleBogey(
    int grossStrokes,
    int par,
    int strokesReceived,
  ) {
    final maxGross = par + 2 + strokesReceived;
    return grossStrokes >= maxGross;
  }

  /// Sums the points across all 18 holes (or however many are supplied).
  static int roundTotal(List<int> pointsPerHole) {
    return pointsPerHole.fold(0, (sum, p) => sum + math.max(0, p));
  }
}
