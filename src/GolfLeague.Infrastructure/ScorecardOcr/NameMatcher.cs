namespace GolfLeague.Infrastructure.ScorecardOcr;

/// <summary>
/// Fuzzy-matches an OCR'd name fragment against the tee-time roster.
/// Handwritten/printed scorecards rarely have a full "First Last" in one
/// cell — often just a first name, last name, or initials — so this checks
/// several cheap heuristics rather than a single edit-distance threshold.
/// </summary>
internal static class NameMatcher
{
    /// <summary>
    /// Returns the best-matching candidate index, or null if nothing clears
    /// the confidence floor. Ties or ambiguous matches are treated as no
    /// match — the confirm screen is where the user resolves ambiguity.
    /// </summary>
    public static int? FindBestMatch(string rawText, IReadOnlyList<(string FullName, string Initials)> candidates)
    {
        var normalized = Normalize(rawText);
        if (normalized.Length == 0)
            return null;

        var scores = new double[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            var (fullName, initials) = candidates[i];
            scores[i] = Score(normalized, Normalize(fullName), Normalize(initials));
        }

        var bestIndex = -1;
        var bestScore = 0.0;
        var secondBestScore = 0.0;
        for (var i = 0; i < scores.Length; i++)
        {
            if (scores[i] > bestScore)
            {
                secondBestScore = bestScore;
                bestScore = scores[i];
                bestIndex = i;
            }
            else if (scores[i] > secondBestScore)
            {
                secondBestScore = scores[i];
            }
        }

        const double minConfidence = 0.5;
        const double minMarginOverRunnerUp = 0.15;
        if (bestIndex < 0 || bestScore < minConfidence || (bestScore - secondBestScore) < minMarginOverRunnerUp)
            return null;

        return bestIndex;
    }

    private static double Score(string ocrText, string fullName, string initials)
    {
        if (ocrText == fullName || ocrText == initials)
            return 1.0;

        if (initials.Length > 0 && ocrText == initials.Replace(".", string.Empty))
            return 0.95;

        var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Any(p => p == ocrText))
            return 0.85;

        var distance = LevenshteinDistance(ocrText, fullName);
        var maxLen = Math.Max(ocrText.Length, fullName.Length);
        if (maxLen == 0)
            return 0;

        var similarity = 1.0 - (double)distance / maxLen;

        // Also try against just the last name, which is the most common
        // single-word entry on a paper scorecard.
        if (nameParts.Length > 0)
        {
            var lastName = nameParts[^1];
            var lastNameDistance = LevenshteinDistance(ocrText, lastName);
            var lastNameMaxLen = Math.Max(ocrText.Length, lastName.Length);
            if (lastNameMaxLen > 0)
                similarity = Math.Max(similarity, 1.0 - (double)lastNameDistance / lastNameMaxLen);
        }

        return similarity;
    }

    private static string Normalize(string value) =>
        new string(value.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c)).ToArray());

    private static int LevenshteinDistance(string a, string b)
    {
        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            costs[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            costs[0] = i;
            var previousDiagonal = i - 1;
            for (var j = 1; j <= b.Length; j++)
            {
                var previousDiagonalSave = costs[j];
                costs[j] = a[i - 1] == b[j - 1]
                    ? previousDiagonal
                    : 1 + Math.Min(previousDiagonal, Math.Min(costs[j], costs[j - 1]));
                previousDiagonal = previousDiagonalSave;
            }
        }

        return costs[b.Length];
    }
}
