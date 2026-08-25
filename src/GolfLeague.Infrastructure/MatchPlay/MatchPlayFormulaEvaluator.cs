using GolfLeague.Domain.Services;
using NCalc;

namespace GolfLeague.Infrastructure.MatchPlay;

/// <summary>
/// NCalc-backed implementation of <see cref="IMatchPlayFormulaEvaluator"/>.
/// Formulas are plain arithmetic/relational expressions over the variables
/// in <see cref="MatchPlayFormulaInput"/> — no NCalc parameters that could
/// reach .NET types/reflection are exposed, so the expression surface is
/// limited to arithmetic (same posture as HandicapFormulaEvaluator).
/// </summary>
public sealed class MatchPlayFormulaEvaluator : IMatchPlayFormulaEvaluator
{
    public double Evaluate(string formula, MatchPlayFormulaInput input)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new FormatException("Formula is empty.");

        var expression = new Expression(formula, ExpressionOptions.NoCache);
        expression.Parameters["netStrokes"] = input.NetStrokes;
        expression.Parameters["opponentNetStrokes"] = input.OpponentNetStrokes;
        expression.Parameters["grossStrokes"] = input.GrossStrokes;
        expression.Parameters["opponentGrossStrokes"] = input.OpponentGrossStrokes;
        expression.Parameters["par"] = input.Par;
        expression.Parameters["strokeIndex"] = input.StrokeIndex;
        expression.Parameters["holeNumber"] = input.HoleNumber;
        expression.Parameters["courseRating"] = input.CourseRating;
        expression.Parameters["slopeRating"] = input.SlopeRating;
        expression.Parameters["handicapIndex"] = input.HandicapIndex;
        expression.Parameters["opponentHandicapIndex"] = input.OpponentHandicapIndex;
        expression.Parameters["isAgainstCard"] = input.IsAgainstCard;

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
            bool b => b ? 1 : 0,
            _ => throw new FormatException("Formula must evaluate to a number or boolean."),
        };
    }

    public bool TryValidate(string formula, out string? error)
    {
        try
        {
            // Sample inputs representative of a real hole, just to exercise
            // the expression and catch parse/eval errors early.
            Evaluate(formula, new MatchPlayFormulaInput(
                NetStrokes: 4, OpponentNetStrokes: 5, GrossStrokes: 5, OpponentGrossStrokes: 6,
                Par: 4, StrokeIndex: 7, HoleNumber: 3, CourseRating: 71.2, SlopeRating: 128,
                HandicapIndex: 12.4, OpponentHandicapIndex: 8.1, IsAgainstCard: false));
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
