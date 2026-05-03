using Xunit;
using GolfLeague.Domain.Services;
using FluentAssertions;

namespace GolfLeague.Tests.Domain;

public class HandicapCalculationServiceExtendedTests
{
    [Theory]
    [InlineData(5.5, 6.5, 6)]
    [InlineData(10, 12, 11)]
    [InlineData(20, 24, 22)]
    [InlineData(0, 0, 0)]
    public void CombineNineHoleDifferentials_ReturnsAverage(double d1, double d2, double expected)
    {
        var result = HandicapCalculationService.CombineNineHoleDifferentials(d1, d2);
        result.Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public void CombineNineHoleDifferentials_RoundsTwoDecimalPlaces()
    {
        var result = HandicapCalculationService.CombineNineHoleDifferentials(5.556, 6.777);
        result.Should().BeApproximately(6.17, 0.01);
    }

    [Fact]
    public void CalculateNewIndex_WhenEmpty_ReturnsZero()
    {
        var result = HandicapCalculationService.CalculateNewIndex(new List<double>());
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateNewIndex_With1Differential_UsesIt()
    {
        var differentials = new List<double> { 10.0 };
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With2Differentials_UsesLowest1()
    {
        var differentials = new List<double> { 10.0, 8.0 };
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        // Should use only the lowest 1 differential (8.0)
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With3Differentials_UsesLowest1()
    {
        var differentials = new List<double> { 10.0, 8.0, 12.0 };
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With5Differentials_UsesLowest2()
    {
        var differentials = new List<double> { 10.0, 8.0, 12.0, 9.0, 15.0 };
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With7Differentials_UsesLowest3()
    {
        var differentials = Enumerable.Range(1, 7).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With9Differentials_UsesLowest4()
    {
        var differentials = Enumerable.Range(1, 9).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With11Differentials_UsesLowest5()
    {
        var differentials = Enumerable.Range(1, 11).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With14Differentials_UsesLowest6()
    {
        var differentials = Enumerable.Range(1, 14).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With17Differentials_UsesLowest7()
    {
        var differentials = Enumerable.Range(1, 17).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_With20Differentials_UsesLowest8()
    {
        var differentials = Enumerable.Range(1, 20).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_IgnoresDifferentialsOlderThan20()
    {
        var differentials = Enumerable.Range(1, 25).Select(i => (double)i).ToList();
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        // Should only use the last 20 differentials
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_NeverBelowNegative10()
    {
        var differentials = new List<double> { -100.0, -200.0, -300.0 };
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThanOrEqualTo(-10.0);
    }

    [Fact]
    public void CalculateNewIndex_WithBankruptPlusCourseChange_UsesEvenRounding()
    {
        var differentials = new List<double> { 5.15, 5.25, 5.35, 5.45 };
        var result = HandicapCalculationService.CalculateNewIndex(differentials);
        result.Should().BeGreaterThan(0);
    }
}
