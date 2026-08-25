namespace GolfLeague.Domain.Services;

/// <summary>
/// Evaluates a league-admin-supplied NCalc formula to produce one player's
/// point total for one hole in a match-play match, as an alternative to the
/// standard 2/1/0 win/halve/loss scoring. See
/// SeasonHalf.MatchPlayCustomFormula.
/// </summary>
public interface IMatchPlayFormulaEvaluator
{
    /// <summary>Evaluates <paramref name="formula"/> for one player on one hole.</summary>
    /// <exception cref="FormatException">The formula is empty, malformed, unsupported, or did not evaluate to a number.</exception>
    double Evaluate(string formula, MatchPlayFormulaInput input);

    /// <summary>Validates that <paramref name="formula"/> parses and evaluates against sample inputs, without throwing.</summary>
    bool TryValidate(string formula, out string? error);
}

/// <summary>
/// Variables available to a custom match-play hole-scoring formula, evaluated
/// once per player per hole. "This player" is the player the formula is
/// being evaluated for; opponent fields reflect the actual opponent, or
/// mirror par / this player's own handicap (per <see cref="IsAgainstCard"/>)
/// when playing against the card due to an absence or bye.
/// </summary>
public readonly record struct MatchPlayFormulaInput(
    int NetStrokes,
    int OpponentNetStrokes,
    int GrossStrokes,
    int OpponentGrossStrokes,
    int Par,
    int StrokeIndex,
    int HoleNumber,
    double CourseRating,
    int SlopeRating,
    double HandicapIndex,
    double OpponentHandicapIndex,
    bool IsAgainstCard);
