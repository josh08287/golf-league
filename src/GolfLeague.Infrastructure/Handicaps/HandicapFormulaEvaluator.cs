using GolfLeague.Domain.Services;
using NCalc;

namespace GolfLeague.Infrastructure.Handicaps;

/// <summary>
/// NCalc-backed implementation of <see cref="IHandicapFormulaEvaluator"/>.
/// Formulas are plain arithmetic expressions (+, -, *, /, parentheses,
/// standard math functions) over the variables in
/// <see cref="HandicapFormulaInput"/> — no NCalc parameters that could
/// reach .NET types/reflection are exposed, so the expression surface is
/// limited to arithmetic.
/// </summary>
public sealed class HandicapFormulaEvaluator : IHandicapFormulaEvaluator
{
    public double Evaluate(string formula, HandicapFormulaInput input)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new FormatException("Formula is empty.");

        var expression = new Expression(formula, ExpressionOptions.NoCache);
        expression.Parameters["grossStrokes"] = input.GrossStrokes;
        expression.Parameters["courseRating"] = input.CourseRating;
        expression.Parameters["slopeRating"] = input.SlopeRating;
        expression.Parameters["par"] = input.Par;

        object? result;
        try
        {
            result = expression.Evaluate();
        }
        catch (Exception ex) when (ex is not FormatException)
        {
            throw new FormatException($"Formula could not be evaluated: {ex.Message}", ex);
        }

        return result switch
        {
            double d => d,
            int i => i,
            decimal dec => (double)dec,
            _ => throw new FormatException("Formula must evaluate to a number."),
        };
    }

    public bool TryValidate(string formula, out string? error)
    {
        try
        {
            // Sample inputs representative of a real 9-hole round, just to
            // exercise the expression and catch parse/eval errors early.
            Evaluate(formula, new HandicapFormulaInput(GrossStrokes: 40, CourseRating: 35.5, SlopeRating: 113, Par: 36));
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
