using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Player>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<Player?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken = default);
    Task AddAsync(Player player, CancellationToken cancellationToken = default);
    Task UpdateAsync(Player player, CancellationToken cancellationToken = default);
    Task AssignToFlightAsync(int playerId, int? flightId, CancellationToken cancellationToken = default);
}
