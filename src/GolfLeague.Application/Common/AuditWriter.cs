using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Common;

/// <summary>
/// Best-effort audit write for mutations that bypass MediatR's AuditBehavior
/// (direct service calls invoked straight from a Function endpoint). An audit
/// write must never fail the operation it's recording, so failures are
/// swallowed and logged rather than propagated — mirrors AuditBehavior's own
/// handling for MediatR-routed commands.
/// </summary>
public sealed class AuditWriter
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(IAuditRepository auditRepository, ILogger<AuditWriter> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        string userId,
        int? leagueId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _auditRepository.AddAsync(new AuditLog
            {
                LeagueId = leagueId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                UserId = userId,
                Timestamp = DateTime.UtcNow,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for {Action}; the operation itself succeeded.", action);
        }
    }
}
