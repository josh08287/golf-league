namespace GolfLeague.Domain.Entities;

/// <summary>
/// A global, application-wide feature toggle — not scoped to any league.
/// Used to roll out new features gradually and kill-switch them if they
/// misbehave, without a deploy.
/// </summary>
public class FeatureFlag
{
    public int Id { get; set; }

    /// <summary>Well-known key, e.g. "self_skip_rounds_enabled".</summary>
    public string Key { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}
