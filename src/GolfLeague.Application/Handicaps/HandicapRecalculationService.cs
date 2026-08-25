using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Leagues;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;

namespace GolfLeague.Application.Handicaps;

/// <summary>
/// Loads the league's handicap-calculation settings once and converts raw
/// round inputs to differentials accordingly, so <c>FinalizeRoundCommand</c>
/// and <c>RecalculateAllHandicapsCommand</c> apply identical rules.
/// </summary>
public sealed class HandicapRecalculationService
{
    private readonly ILeagueSettingRepository _settings;
    private readonly IHandicapFormulaEvaluator _formulaEvaluator;

    public HandicapRecalculationService(ILeagueSettingRepository settings, IHandicapFormulaEvaluator formulaEvaluator)
    {
        _settings = settings;
        _formulaEvaluator = formulaEvaluator;
    }

    public async Task<HandicapCalcSettings> LoadSettingsAsync(int leagueId, CancellationToken cancellationToken)
    {
        var rows = await _settings.GetAllAsync(leagueId, cancellationToken);

        string Get(string key) => rows.FirstOrDefault(r => r.Key == key)?.Value ?? KnownSettings.Defaults[key];

        var mode = Get(KnownSettings.HandicapCalcMode) switch
        {
            KnownSettings.HandicapModeStraightStrokes => HandicapDifferentialMode.StraightStrokes,
            KnownSettings.HandicapModeCustom => HandicapDifferentialMode.Custom,
            _ => HandicapDifferentialMode.Usga,
        };

        var windowX = int.TryParse(Get(KnownSettings.HandicapWindowX), out var x) && x >= 1
            ? x : HandicapCalculationService.DefaultWindowX;
        var windowY = int.TryParse(Get(KnownSettings.HandicapWindowY), out var y) && y >= 1
            ? y : HandicapCalculationService.DefaultWindowY;

        return new HandicapCalcSettings(mode, windowX, windowY, Get(KnownSettings.HandicapCustomFormula));
    }

    /// <summary>
    /// Computes the player's new 18-hole handicap index from up to
    /// <paramref name="settings"/>.WindowY qualifying round inputs (newest
    /// first), or <c>null</c> if there are no qualifying rounds.
    /// </summary>
    public double? CalculateNewIndex(IReadOnlyList<HandicapRoundInput> roundInputs, HandicapCalcSettings settings)
    {
        if (roundInputs.Count == 0) return null;

        var pool = roundInputs.Count <= settings.WindowY
            ? roundInputs
            : roundInputs.Take(settings.WindowY).ToList();

        var differentials = pool.Select(r => ComputeDifferential(r, settings)).ToList();

        var nineHoleIndex = HandicapCalculationService.CalculateNewIndex(differentials, settings.WindowX, settings.WindowY);
        return Math.Round(nineHoleIndex * 2, 1, MidpointRounding.ToEven);
    }

    /// <summary>Computes a single round's differential per the league's configured mode — used for display (e.g. player round history).</summary>
    public double ComputeDifferential(HandicapRoundInput round, HandicapCalcSettings settings) => settings.Mode switch
    {
        HandicapDifferentialMode.StraightStrokes =>
            StablefordScoringService.NineHoleStraightStrokesDifferential(round.GrossStrokes, round.CourseRating),
        HandicapDifferentialMode.Custom when !string.IsNullOrWhiteSpace(settings.CustomFormula) =>
            _formulaEvaluator.Evaluate(settings.CustomFormula, new HandicapFormulaInput(round.GrossStrokes, round.CourseRating, round.SlopeRating, round.Par)),
        _ => StablefordScoringService.NineHoleScoreDifferential(round.GrossStrokes, round.CourseRating, round.SlopeRating),
    };
}

public sealed record HandicapCalcSettings(HandicapDifferentialMode Mode, int WindowX, int WindowY, string CustomFormula);
