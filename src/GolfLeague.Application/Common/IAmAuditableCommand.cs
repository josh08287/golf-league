namespace GolfLeague.Application.Common;

/// <summary>
/// Marks a MediatR command as one that should produce an AuditLog row on
/// success. EntityType/EntityId are supplied explicitly by each command
/// (rather than inferred by reflection) so every command's audit entry is
/// deliberate and resolvable by GetAuditLogQueryHandler's display-name
/// lookup instead of silently falling through to "Unknown".
/// </summary>
public interface IAmAuditableCommand
{
    string UserId { get; }
    string AuditEntityType { get; }
    string AuditEntityId { get; }
}
