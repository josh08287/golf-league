using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface ILeagueRepository
{
    Task<League?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<League?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<League>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LeagueMembership?> GetMembershipAsync(int leagueId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(League league, CancellationToken cancellationToken = default);
    Task UpdateAsync(League league, CancellationToken cancellationToken = default);
}
