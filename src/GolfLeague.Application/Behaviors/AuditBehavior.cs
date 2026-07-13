using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(IAuditRepository auditRepository, ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _auditRepository = auditRepository;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is IAmAuditableCommand auditable)
        {
            var succeeded = response is not null && IsSuccessResult(response);

            if (succeeded)
            {
                try
                {
                    var entityId = auditable.AuditEntityId;
                    if (entityId == "0")
                        entityId = TryResolveCreatedId(response) ?? entityId;

                    var auditLog = new AuditLog
                    {
                        Action = typeof(TRequest).Name,
                        EntityType = auditable.AuditEntityType,
                        EntityId = entityId,
                        UserId = auditable.UserId,
                        Timestamp = DateTime.UtcNow
                    };

                    await _auditRepository.AddAsync(auditLog, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to write audit log for {Action}; the operation itself succeeded.", typeof(TRequest).Name);
                }
            }
        }

        return response;
    }

    private static bool IsSuccessResult(TResponse response)
    {
        var type = typeof(TResponse);
        if (!type.IsGenericType)
            return true;

        var prop = type.GetProperty(nameof(Result<object>.IsSuccess));
        return prop?.GetValue(response) is true;
    }

    /// <summary>
    /// Creation commands don't know their new row's id until after Handle
    /// runs, so AuditEntityId is "0" (a sentinel, not a real id) for those.
    /// Falls back to reflecting Result&lt;T&gt;.Value.Id off the response so
    /// created-entity audit rows still resolve to a real id instead of "0".
    /// </summary>
    private static string? TryResolveCreatedId(TResponse response)
    {
        var valueProp = typeof(TResponse).GetProperty(nameof(Result<object>.Value));
        var value = valueProp?.GetValue(response);
        if (value is null) return null;

        var idProp = value.GetType().GetProperty("Id");
        if (idProp is not null)
            return idProp.GetValue(value)?.ToString();

        // Nested-DTO shapes (e.g. TournamentRoundDto.Round.Id) — look one level down
        // for a property whose own type exposes "Id".
        foreach (var prop in value.GetType().GetProperties())
        {
            var nested = prop.GetValue(value);
            var nestedId = nested?.GetType().GetProperty("Id")?.GetValue(nested);
            if (nestedId is not null)
                return nestedId.ToString();
        }

        return null;
    }
}
