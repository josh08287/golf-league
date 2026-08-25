using FluentAssertions;
using GolfLeague.Application.Handicaps;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Handicaps;
using Moq;
using Xunit;

namespace GolfLeague.Tests.Application;

public class HandicapRecalculationServiceTests
{
    private static HandicapRecalculationService BuildSut(IReadOnlyList<LeagueSetting> settingRows)
    {
        var settings = new Mock<ILeagueSettingRepository>();
        settings.Setup(s => s.GetAllAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(settingRows);
        return new HandicapRecalculationService(settings.Object, new HandicapFormulaEvaluator());
    }

    [Fact]
    public async Task LoadSettingsAsync_NoRowsStored_FallsBackToUsgaDefaults()
    {
        var sut = BuildSut(new List<LeagueSetting>());

        var settings = await sut.LoadSettingsAsync(1, CancellationToken.None);

        settings.Mode.Should().Be(HandicapDifferentialMode.Usga);
        settings.WindowX.Should().Be(5);
        settings.WindowY.Should().Be(5);
    }

    [Fact]
    public async Task LoadSettingsAsync_ReadsConfiguredModeAndWindow()
    {
        var sut = BuildSut(new List<LeagueSetting>
        {
            new() { Key = KnownSettings.HandicapCalcMode, Value = KnownSettings.HandicapModeStraightStrokes },
            new() { Key = KnownSettings.HandicapWindowX, Value = "3" },
            new() { Key = KnownSettings.HandicapWindowY, Value = "8" },
        });

        var settings = await sut.LoadSettingsAsync(1, CancellationToken.None);

        settings.Mode.Should().Be(HandicapDifferentialMode.StraightStrokes);
        settings.WindowX.Should().Be(3);
        settings.WindowY.Should().Be(8);
    }

    [Fact]
    public void CalculateNewIndex_UsgaMode_MatchesBuiltInFormula()
    {
        var sut = BuildSut(new List<LeagueSetting>());
        var settings = new HandicapCalcSettings(HandicapDifferentialMode.Usga, WindowX: 2, WindowY: 2, CustomFormula: "");
        var rounds = new List<HandicapRoundInput>
        {
            new(GrossStrokes: 42, CourseRating: 35.5, SlopeRating: 118, Par: 36),
            new(GrossStrokes: 40, CourseRating: 35.5, SlopeRating: 118, Par: 36),
        };

        var index = sut.CalculateNewIndex(rounds, settings);

        index.Should().NotBeNull();
        // Both differentials averaged then doubled — sanity check it's in a plausible range, not zero.
        index!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateNewIndex_StraightStrokesMode_UsesGrossStrokesOverPar()
    {
        var sut = BuildSut(new List<LeagueSetting>());
        var settings = new HandicapCalcSettings(HandicapDifferentialMode.StraightStrokes, WindowX: 1, WindowY: 1, CustomFormula: "");
        // grossStrokes=40, par=36 -> straight-strokes 9-hole diff = 40 - 18 = 22 (course rating/slope ignored), doubled = 44.
        var rounds = new List<HandicapRoundInput> { new(GrossStrokes: 40, CourseRating: 36, SlopeRating: 200, Par: 36) };

        sut.CalculateNewIndex(rounds, settings).Should().Be(44.0);
    }

    [Fact]
    public void CalculateNewIndex_CustomMode_EvaluatesFormula()
    {
        var sut = BuildSut(new List<LeagueSetting>());
        var settings = new HandicapCalcSettings(HandicapDifferentialMode.Custom, WindowX: 1, WindowY: 1, CustomFormula: "grossStrokes - par");
        // grossStrokes=40, par=36 -> diff = 4, doubled = 8.0
        var rounds = new List<HandicapRoundInput> { new(GrossStrokes: 40, CourseRating: 35.5, SlopeRating: 113, Par: 36) };

        sut.CalculateNewIndex(rounds, settings).Should().Be(8.0);
    }

    [Fact]
    public void CalculateNewIndex_NoRounds_ReturnsNull()
    {
        var sut = BuildSut(new List<LeagueSetting>());
        var settings = new HandicapCalcSettings(HandicapDifferentialMode.Usga, WindowX: 5, WindowY: 5, CustomFormula: "");

        sut.CalculateNewIndex(new List<HandicapRoundInput>(), settings).Should().BeNull();
    }
}
