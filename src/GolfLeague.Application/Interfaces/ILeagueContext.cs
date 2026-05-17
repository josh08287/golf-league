namespace GolfLeague.Application.Interfaces;

/// <summary>
/// Ambient per-request league context resolved from the JWT leagueId claim.
/// Set by LeagueContextMiddleware before any handler runs.
/// </summary>
public interface ILeagueContext
{
    int? LeagueId { get; }
    bool IsSuperAdmin { get; }
    /// <summary>True once LeagueContextMiddleware has called Set() for this request.</summary>
    bool IsSet { get; }

    void Set(int? leagueId, bool isSuperAdmin);
}
