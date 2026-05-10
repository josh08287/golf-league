using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Entities;

public class Handicap
{
    public int Id { get; set; }
    public int PlayerId { get; set; }

    /// <summary>
    /// The full 18-hole handicap index. This is the value that gets
    /// edited / persisted; the 9-hole value is derived from it.
    /// </summary>
    public double HandicapIndex { get; set; }

    /// <summary>
    /// Half of the 18-hole index — what gets applied for the league's
    /// 9-hole rounds. Computed, never stored, so the two can never drift.
    /// </summary>
    public double NineHoleHandicapIndex => HandicapIndex / 2.0;

    public DateOnly EffectiveDate { get; set; }
    public HandicapSource Source { get; set; }
    public string? Notes { get; set; }

    public Player Player { get; set; } = null!;
}
