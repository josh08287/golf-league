using FluentAssertions;
using GolfLeague.Domain.Services;
using GolfLeague.Infrastructure.MatchPlay;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

public class MatchPlayFormulaEvaluatorTests
{
    private readonly MatchPlayFormulaEvaluator _sut = new();

    private static MatchPlayFormulaInput MakeInput(
        int netStrokes = 4, int opponentNetStrokes = 5, int grossStrokes = 5, int opponentGrossStrokes = 6,
        int par = 4, int strokeIndex = 7, int holeNumber = 3, double courseRating = 71.2, int slopeRating = 128,
        double handicapIndex = 12.4, double opponentHandicapIndex = 8.1, bool isAgainstCard = false)
        => new(netStrokes, opponentNetStrokes, grossStrokes, opponentGrossStrokes, par, strokeIndex, holeNumber,
            courseRating, slopeRating, handicapIndex, opponentHandicapIndex, isAgainstCard);

    [Fact]
    public void Evaluate_StandardEquivalentFormula_MatchesMatchPlayScoringService()
    {
        var input = MakeInput(netStrokes: 3, opponentNetStrokes: 4);
        var expected = MatchPlayScoringService.HolePoints(3, 4).PlayerPoints;

        var actual = _sut.Evaluate("netStrokes < opponentNetStrokes ? 2 : (netStrokes > opponentNetStrokes ? 0 : 1)", input);

        actual.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_UsesParVariable()
    {
        var input = MakeInput(netStrokes: 3, par: 4);
        _sut.Evaluate("par - netStrokes", input).Should().Be(1);
    }

    [Fact]
    public void Evaluate_UsesStrokeIndexAndHoleNumberVariables()
    {
        var input = MakeInput(strokeIndex: 7, holeNumber: 3);
        _sut.Evaluate("strokeIndex + holeNumber", input).Should().Be(10);
    }

    [Fact]
    public void Evaluate_UsesHandicapVariables()
    {
        var input = MakeInput(handicapIndex: 12.0, opponentHandicapIndex: 8.0);
        _sut.Evaluate("handicapIndex - opponentHandicapIndex", input).Should().Be(4.0);
    }

    [Fact]
    public void Evaluate_UsesIsAgainstCardVariable()
    {
        var input = MakeInput(isAgainstCard: true);
        _sut.Evaluate("isAgainstCard", input).Should().Be(1);
    }

    [Fact]
    public void Evaluate_UsesCourseAndSlopeRatingVariables()
    {
        var input = MakeInput(courseRating: 71.2, slopeRating: 128);
        _sut.Evaluate("slopeRating > 100 && courseRating > 70", input).Should().Be(1);
    }

    [Fact]
    public void Evaluate_UsesGrossStrokesVariables()
    {
        var input = MakeInput(grossStrokes: 5, opponentGrossStrokes: 6);
        _sut.Evaluate("opponentGrossStrokes - grossStrokes", input).Should().Be(1);
    }

    [Fact]
    public void Evaluate_EmptyFormula_Throws()
    {
        var act = () => _sut.Evaluate("", MakeInput());
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Evaluate_MalformedFormula_ThrowsFormatException()
    {
        var act = () => _sut.Evaluate("netStrokes +", MakeInput());
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Evaluate_UnknownVariable_ThrowsFormatException()
    {
        var act = () => _sut.Evaluate("netStrokes - unknownVar", MakeInput());
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Evaluate_BooleanResult_MapsToOneOrZero()
    {
        _sut.Evaluate("netStrokes < opponentNetStrokes", MakeInput(netStrokes: 3, opponentNetStrokes: 5)).Should().Be(1);
        _sut.Evaluate("netStrokes > opponentNetStrokes", MakeInput(netStrokes: 3, opponentNetStrokes: 5)).Should().Be(0);
    }

    [Fact]
    public void TryValidate_ValidFormula_ReturnsTrue()
    {
        _sut.TryValidate("netStrokes < opponentNetStrokes ? 2 : 0", out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_InvalidFormula_ReturnsFalseWithError()
    {
        _sut.TryValidate("netStrokes +++ 1", out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }
}
