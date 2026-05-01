using GolfLeague.Domain.Entities;

namespace GolfLeague.Domain.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
