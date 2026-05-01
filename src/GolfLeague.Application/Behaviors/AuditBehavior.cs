using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditRepository _auditRepository;

    public AuditBehavior(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
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
                var auditLog = new AuditLog
                {
                    Action = typeof(TRequest).Name,
                    EntityType = ResolveEntityType(typeof(TRequest).Name),
                    EntityId = ResolveEntityId(request),
                    UserId = auditable.UserId,
                    Timestamp = DateTime.UtcNow
                };

                await _auditRepository.AddAsync(auditLog, cancellationToken);
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

    private static string ResolveEntityType(string requestName)
    {
        if (requestName.Contains("Player") || requestName.Contains("Handicap"))
            return "Player";
        if (requestName.Contains("Round") || requestName.Contains("HoleScore") || requestName.Contains("Scorecard"))
            return "Round";
        if (requestName.Contains("Flight"))
            return "Flight";
        if (requestName.Contains("Course"))
            return "Course";
        return "Unknown";
    }

    private static string ResolveEntityId(TRequest request)
    {
        var idProp = typeof(TRequest).GetProperty("Id")
            ?? typeof(TRequest).GetProperty("PlayerId")
            ?? typeof(TRequest).GetProperty("RoundId");

        return idProp?.GetValue(request)?.ToString() ?? "0";
    }
}
