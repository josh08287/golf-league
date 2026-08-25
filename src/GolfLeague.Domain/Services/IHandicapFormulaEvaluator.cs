namespace GolfLeague.Domain.Services;

/// <summary>
/// Evaluates a league-admin-supplied arithmetic formula to produce a single
/// round's 9-hole score differential, as an alternative to the built-in USGA
/// or straight-strokes formulas. See LeagueSettings.handicap_custom_formula.
/// </summary>
public interface IHandicapFormulaEvaluator
{
    /// <summary>
    /// Evaluates <paramref name="formula"/> against the given round variables.
    /// Available variables: grossStrokes, courseRating, slopeRating, par
    /// (all referring to the 9-hole side played).
    /// </summary>
    /// <exception cref="FormatException">The formula is empty, malformed, unsupported, or did not evaluate to a number.</exception>
    double Evaluate(string formula, HandicapFormulaInput input);

    /// <summary>Validates that <paramref name="formula"/> parses and evaluates against sample inputs, without throwing.</summary>
    bool TryValidate(string formula, out string? error);
}

/// <summary>Variables available to a custom handicap differential formula, for one round.</summary>
public readonly record struct HandicapFormulaInput(int GrossStrokes, double CourseRating, int SlopeRating, int Par);
