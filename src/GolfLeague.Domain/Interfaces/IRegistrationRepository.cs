using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;

namespace GolfLeague.Domain.Interfaces;

public interface IRegistrationRepository
{
    Task<PlayerRegistration?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PlayerRegistration?> GetByEntraObjectIdAsync(string entraObjectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlayerRegistration>> GetByStatusAsync(RegistrationStatus status, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerRegistration registration, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlayerRegistration registration, CancellationToken cancellationToken = default);
}
