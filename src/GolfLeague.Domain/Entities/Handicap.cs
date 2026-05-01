using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class Handicap
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public double HandicapIndex { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public HandicapSource Source { get; set; }
    public string? Notes { get; set; }

    public Player Player { get; set; } = null!;
}
