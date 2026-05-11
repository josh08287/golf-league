using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Interfaces;

public interface IInviteRepository
{
    Task<PlayerInvite?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PlayerInvite?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerInvite>> GetByStatusAsync(InviteStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerInvite>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> PendingInviteExistsForEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<PlayerInvite?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerInvite invite, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<PlayerInvite> invites, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlayerInvite invite, CancellationToken cancellationToken = default);
    Task DeleteAsync(PlayerInvite invite, CancellationToken cancellationToken = default);
}
