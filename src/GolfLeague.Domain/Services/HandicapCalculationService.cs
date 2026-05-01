namespace GolfLeague.Domain.Services;

public static class HandicapCalculationService
{
    private const int MaxDifferentials = 20;
    private const int BestDifferentialsToUse = 8;
    private const double WhsMultiplier = 0.96;

    public static double CalculateNewIndex(IEnumerable<double> last20Differentials)
    {
        var differentials = last20Differentials.ToList();

        if (differentials.Count == 0)
            return 0.0;

        var count = Math.Min(differentials.Count, MaxDifferentials);
        var recent = differentials.TakeLast(count).ToList();

        var best = SelectBestDifferentials(recent);

        var average = best.Average();
        var index = Math.Round(average * WhsMultiplier, 1, MidpointRounding.ToEven);

        return Math.Max(index, -10.0);
    }

    private static IEnumerable<double> SelectBestDifferentials(List<double> differentials)
    {
        var count = differentials.Count;
        var bestCount = count switch
        {
            >= 20 => BestDifferentialsToUse,
            >= 17 => 7,
            >= 14 => 6,
            >= 11 => 5,
            >= 9 => 4,
            >= 7 => 3,
            >= 5 => 2,
            >= 3 => 1,
            _ => 1
        };

        return differentials.OrderBy(d => d).Take(bestCount);
    }
}
