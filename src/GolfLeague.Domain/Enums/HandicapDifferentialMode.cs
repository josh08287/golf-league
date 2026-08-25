namespace GolfLeague.Domain.Enums;

/// <summary>
/// How a single round's 9-hole score differential is computed, before the
/// rolling best-X-of-Y average is applied. See
/// <see cref="GolfLeague.Domain.Services.HandicapCalculationService"/>.
/// </summary>
public enum HandicapDifferentialMode
{
    /// <summary>USGA-style: (grossStrokes - courseRating) * 113 / slopeRating.</summary>
    Usga,

    /// <summary>Straight strokes over course rating, ignoring slope: grossStrokes - courseRating.</summary>
    StraightStrokes,

    /// <summary>League-admin-supplied formula, evaluated per round. See LeagueSettings.handicap_custom_formula.</summary>
    Custom,
}
