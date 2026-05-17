using GolfLeague.Application.Interfaces;

namespace GolfLeague.Infrastructure.Data;

public sealed class LeagueContext : ILeagueContext
{
    public int? LeagueId { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public bool IsSet { get; private set; }

    public void Set(int? leagueId, bool isSuperAdmin)
    {
        LeagueId = leagueId;
        IsSuperAdmin = isSuperAdmin;
        IsSet = true;
    }
}
