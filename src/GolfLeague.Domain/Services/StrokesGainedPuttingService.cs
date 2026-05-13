using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Services;

/// <summary>
/// Computes Strokes Gained: Putting relative to a baseline (flight average).
///
/// For each hole where putt data is recorded:
///   SG Putting = (baseline avg putts from that distance bucket) – (actual putts)
///
/// Positive = fewer putts than baseline (good). Negative = more putts (bad).
///
/// When no first-putt distance is provided but putts are recorded, a flat
/// baseline of 2.0 putts is used (the PGA Tour average from the green).
/// </summary>
public static class StrokesGainedPuttingService
{
    /// <summary>
    /// Standard distance buckets in feet.
    /// </summary>
    private static readonly (double MinFeet, double MaxFeet, double DefaultBaseline)[] Buckets =
    [
        (0,   3,  1.00),
        (3,   5,  1.05),
        (5,  10,  1.35),
        (10, 20,  1.70),
        (20, 30,  1.90),
        (30, 50,  2.10),
        (50, double.MaxValue, 2.30),
    ];

    /// <summary>
    /// Return the bucket index for a first-putt distance.
    /// </summary>
    public static int GetDistanceBucket(double distanceFeet)
    {
        for (var i = 0; i < Buckets.Length; i++)
        {
            if (distanceFeet >= Buckets[i].MinFeet && distanceFeet < Buckets[i].MaxFeet)
                return i;
        }
        return Buckets.Length - 1;
    }

    /// <summary>
    /// Returns the default (PGA-sourced) baseline for a bucket when no
    /// flight data is available yet.
    /// </summary>
    public static double GetDefaultBaseline(int bucketIndex)
        => Buckets[Math.Clamp(bucketIndex, 0, Buckets.Length - 1)].DefaultBaseline;

    /// <summary>
    /// Compute per-hole SG Putting for a set of hole scores, using flight
    /// averages as the baseline. Returns the total SG Putting and per-round
    /// average.
    /// </summary>
    public static StrokesGainedResult Calculate(
        IReadOnlyList<HoleScore> playerHoleScores,
        IReadOnlyList<HoleScore> flightHoleScores)
    {
        // Build flight baselines: for each distance bucket, the average putts
        var flightBucketTotals = new double[Buckets.Length];
        var flightBucketCounts = new int[Buckets.Length];

        foreach (var hs in flightHoleScores)
        {
            if (hs.Putts is null) continue;

            if (hs.FirstPuttDistanceFeet is not null)
            {
                var bucket = GetDistanceBucket(hs.FirstPuttDistanceFeet.Value);
                flightBucketTotals[bucket] += hs.Putts.Value;
                flightBucketCounts[bucket]++;
            }
            else
            {
                // No distance → flat "from anywhere on green"
                // Spread across all buckets is not useful; treat as bucket-less
                // and use default baseline for this score
            }
        }

        var flightBaselines = new double[Buckets.Length];
        for (var i = 0; i < Buckets.Length; i++)
        {
            flightBaselines[i] = flightBucketCounts[i] >= 3
                ? flightBucketTotals[i] / flightBucketCounts[i]
                : GetDefaultBaseline(i);
        }

        // Flat flight average putts (for scores without distance data)
        var allFlightPutts = flightHoleScores
            .Where(hs => hs.Putts.HasValue)
            .Select(hs => hs.Putts!.Value)
            .ToList();
        var flatFlightBaseline = allFlightPutts.Count >= 5
            ? allFlightPutts.Average()
            : 2.0;

        // Calculate SG Putting for each of the player's holes
        double totalSg = 0;
        int holesWithPutts = 0;

        foreach (var hs in playerHoleScores)
        {
            if (hs.Putts is null) continue;
            holesWithPutts++;

            double baseline;
            if (hs.FirstPuttDistanceFeet is not null)
            {
                var bucket = GetDistanceBucket(hs.FirstPuttDistanceFeet.Value);
                baseline = flightBaselines[bucket];
            }
            else
            {
                baseline = flatFlightBaseline;
            }

            totalSg += baseline - hs.Putts.Value;
        }

        return new StrokesGainedResult(
            totalSg,
            holesWithPutts > 0 ? totalSg / holesWithPutts : 0,
            holesWithPutts);
    }
}

public sealed record StrokesGainedResult(
    double TotalStrokesGained,
    double PerHoleAverage,
    int HolesWithPuttData);
