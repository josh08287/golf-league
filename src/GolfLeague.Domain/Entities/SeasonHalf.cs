using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class SeasonHalf
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int HalfNumber { get; set; } // 1 or 2
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public ScoringFormat ScoringFormat { get; set; } = ScoringFormat.Stableford;

    /// <summary>
    /// Match-play only. Null/empty = use standard scoring (2/1/0 per hole +
    /// 4-point match bonus). When set, this NCalc expression is evaluated
    /// once per hole per player via IMatchPlayFormulaEvaluator instead of
    /// the standard point values. See MatchPlayScoringService/MatchPlayFormulaEvaluator.
    /// </summary>
    public string? MatchPlayCustomFormula { get; set; }

    public Season Season { get; set; } = null!;
    public ICollection<Flight> Flights { get; set; } = [];
    public ICollection<Round> Rounds { get; set; } = [];
    public ICollection<FlightMatch> Matches { get; set; } = [];
}
