namespace GolfLeague.Domain.Entities;

/// <summary>
/// A single key/value configuration entry scoped to a league.
/// Values are stored as strings; callers cast to the appropriate type.
/// </summary>
public class LeagueSetting
{
    public int Id { get; set; }
    public int LeagueId { get; set; }

    /// <summary>Well-known key, e.g. "tee_time_email_enabled".</summary>
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public League League { get; set; } = null!;
}
