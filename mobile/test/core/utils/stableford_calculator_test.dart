import 'package:flutter_test/flutter_test.dart';
import 'package:golf_league/core/utils/stableford_calculator.dart';

void main() {
  group('StablefordCalculator.points', () {
    test('returns 0 for double bogey (net = par + 2)', () {
      expect(StablefordCalculator.points(6, 4), 0);
    });

    test('returns 0 for worse than double bogey', () {
      expect(StablefordCalculator.points(8, 4), 0);
      expect(StablefordCalculator.points(10, 5), 0);
    });

    test('returns 1 for bogey (net = par + 1)', () {
      expect(StablefordCalculator.points(5, 4), 1);
      expect(StablefordCalculator.points(4, 3), 1);
    });

    test('returns 2 for par (net = par)', () {
      expect(StablefordCalculator.points(4, 4), 2);
      expect(StablefordCalculator.points(3, 3), 2);
      expect(StablefordCalculator.points(5, 5), 2);
    });

    test('returns 3 for birdie (net = par - 1)', () {
      expect(StablefordCalculator.points(3, 4), 3);
      expect(StablefordCalculator.points(2, 3), 3);
    });

    test('returns 4 for eagle (net = par - 2)', () {
      expect(StablefordCalculator.points(2, 4), 4);
      expect(StablefordCalculator.points(3, 5), 4);
    });

    test('returns 5 for albatross (net = par - 3)', () {
      expect(StablefordCalculator.points(2, 5), 5);
      expect(StablefordCalculator.points(0, 3), 5);
    });

    test('returns 6 for condor or better (net <= par - 4)', () {
      expect(StablefordCalculator.points(1, 5), 6);
      expect(StablefordCalculator.points(0, 5), 6);
      expect(StablefordCalculator.points(-1, 5), 6);
    });
  });

  group('StablefordCalculator.courseHandicap', () {
    test('calculates correctly for typical index and slope', () {
      // 14.0 * 128 / 113 = 15.84 → rounds to 16
      expect(StablefordCalculator.courseHandicap(14.0, 128), 16);
    });

    test('scratch golfer gets 0 strokes on standard slope', () {
      expect(StablefordCalculator.courseHandicap(0.0, 113), 0);
    });

    test('standard slope returns index rounded', () {
      // 18.0 * 113 / 113 = 18.0
      expect(StablefordCalculator.courseHandicap(18.0, 113), 18);
    });

    test('rounds half-values up', () {
      // 9.0 * 113 / 113 = 9.0 exactly
      expect(StablefordCalculator.courseHandicap(9.0, 113), 9);
    });
  });

  group('StablefordCalculator.strokesOnHole', () {
    test('14 HC: receives stroke on SI 1-14', () {
      expect(StablefordCalculator.strokesOnHole(14, 1), 1);
      expect(StablefordCalculator.strokesOnHole(14, 9), 1);
      expect(StablefordCalculator.strokesOnHole(14, 14), 1);
    });

    test('14 HC: receives no stroke on SI 15-18', () {
      expect(StablefordCalculator.strokesOnHole(14, 15), 0);
      expect(StablefordCalculator.strokesOnHole(14, 18), 0);
    });

    test('0 HC: receives no strokes on any hole', () {
      for (int si = 1; si <= 18; si++) {
        expect(StablefordCalculator.strokesOnHole(0, si), 0,
            reason: 'SI $si');
      }
    });

    test('18 HC: receives exactly 1 stroke on every hole', () {
      for (int si = 1; si <= 18; si++) {
        expect(StablefordCalculator.strokesOnHole(18, si), 1,
            reason: 'SI $si');
      }
    });

    test('20 HC: receives 2 strokes on SI 1-2, 1 stroke on SI 3-18', () {
      // floor(20/18)=1, 20%18=2 → extra stroke for SI <=2
      expect(StablefordCalculator.strokesOnHole(20, 1), 2);
      expect(StablefordCalculator.strokesOnHole(20, 2), 2);
      expect(StablefordCalculator.strokesOnHole(20, 3), 1);
      expect(StablefordCalculator.strokesOnHole(20, 18), 1);
    });

    test('36 HC: receives exactly 2 strokes on every hole', () {
      for (int si = 1; si <= 18; si++) {
        expect(StablefordCalculator.strokesOnHole(36, si), 2,
            reason: 'SI $si');
      }
    });
  });

  group('StablefordCalculator.netStrokes', () {
    test('subtracts strokes received from gross', () {
      expect(StablefordCalculator.netStrokes(5, 1), 4);
      expect(StablefordCalculator.netStrokes(4, 0), 4);
      expect(StablefordCalculator.netStrokes(6, 2), 4);
    });
  });

  group('StablefordCalculator.isNetDoubleBogey', () {
    test('returns true when gross equals max (par+2+strokes)', () {
      // par=4, strokes=1 → max = 7
      expect(StablefordCalculator.isNetDoubleBogey(7, 4, 1), isTrue);
    });

    test('returns true when gross exceeds max', () {
      expect(StablefordCalculator.isNetDoubleBogey(8, 4, 1), isTrue);
    });

    test('returns false when gross is one under max', () {
      // par=4, strokes=1 → max = 7; gross=6 is under
      expect(StablefordCalculator.isNetDoubleBogey(6, 4, 1), isFalse);
    });

    test('scratch golfer: max = par + 2', () {
      // par=4, strokes=0 → max=6
      expect(StablefordCalculator.isNetDoubleBogey(6, 4, 0), isTrue);
      expect(StablefordCalculator.isNetDoubleBogey(5, 4, 0), isFalse);
    });
  });

  group('StablefordCalculator.roundTotal', () {
    test('sums all points correctly', () {
      expect(StablefordCalculator.roundTotal([2, 2, 2, 2, 2, 2, 2, 2, 2]), 18);
    });

    test('ignores negative values (treats as 0)', () {
      expect(StablefordCalculator.roundTotal([-1, 2, 3]), 5);
    });

    test('empty list returns 0', () {
      expect(StablefordCalculator.roundTotal([]), 0);
    });

    test('typical round score', () {
      // 18 holes, mix of 0,1,2,3,4 points
      final scores = [2, 1, 3, 2, 0, 2, 3, 1, 2, 2, 3, 2, 1, 2, 2, 0, 3, 2];
      expect(StablefordCalculator.roundTotal(scores), 33);
    });
  });
}
