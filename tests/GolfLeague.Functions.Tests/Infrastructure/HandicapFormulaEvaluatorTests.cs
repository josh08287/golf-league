using FluentAssertions;
using GolfLeague.Domain.Services;
using GolfLeague.Infrastructure.Handicaps;
using Xunit;

namespace GolfLeague.Tests.Infrastructure;

public class HandicapFormulaEvaluatorTests
{
    private readonly HandicapFormulaEvaluator _sut = new();

    [Fact]
    public void Evaluate_UsgaEquivalentFormula_MatchesBuiltInUsga()
    {
        var input = new HandicapFormulaInput(GrossStrokes: 42, CourseRating: 35.5, SlopeRating: 118, Par: 36);
        var expected = StablefordScoringService.NineHoleScoreDifferential(42, 35.5, 118);

        var actual = _sut.Evaluate("(grossStrokes - courseRating / 2) * (113 / slopeRating)", input);

        actual.Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void Evaluate_UsesParVariable()
    {
        var input = new HandicapFormulaInput(GrossStrokes: 40, CourseRating: 35.5, SlopeRating: 113, Par: 36);
        _sut.Evaluate("grossStrokes - par", input).Should().Be(4);
    }

    [Fact]
    public void Evaluate_EmptyFormula_Throws()
    {
        var act = () => _sut.Evaluate("", new HandicapFormulaInput(40, 35.5, 113, 36));
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Evaluate_MalformedFormula_ThrowsFormatException()
    {
        var act = () => _sut.Evaluate("grossStrokes +", new HandicapFormulaInput(40, 35.5, 113, 36));
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Evaluate_UnknownVariable_ThrowsFormatException()
    {
        var act = () => _sut.Evaluate("grossStrokes - unknownVar", new HandicapFormulaInput(40, 35.5, 113, 36));
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryValidate_ValidFormula_ReturnsTrue()
    {
        _sut.TryValidate("grossStrokes - courseRating", out var error).Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void TryValidate_InvalidFormula_ReturnsFalseWithError()
    {
        _sut.TryValidate("grossStrokes +++ 1", out var error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }
}
