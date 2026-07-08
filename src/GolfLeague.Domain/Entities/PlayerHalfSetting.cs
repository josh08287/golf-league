namespace GolfLeague.Domain.Entities;

/// <summary>
/// Per-player, per-half opt-in settings. Deliberately separate from
/// FlightMembership, whose rows are deleted and recreated whenever an
/// admin reassigns a player's flight — settings stored here survive
/// that churn.
/// </summary>
public class PlayerHalfSetting
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int HalfId { get; set; }
    public int SeasonId { get; set; }

    /// <summary>
    /// When true, the player is eligible to win par-3 gross skins for this
    /// half. Defaults to true so existing behavior is unchanged until an
    /// admin explicitly opts a player out.
    /// </summary>
    public bool Par3GrossSkinsOptIn { get; set; } = true;

    public Player Player { get; set; } = null!;
    public SeasonHalf Half { get; set; } = null!;
}
