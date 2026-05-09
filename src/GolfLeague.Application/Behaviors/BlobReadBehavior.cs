using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Behaviors;

/// <summary>
/// Wraps every MediatR request that is NOT a write command (i.e., not
/// <see cref="IAmAuditableCommand"/>) and refreshes the local SQLite file
/// from blob storage if the remote has changed. When invoked from inside
/// an active write scope, the refresh is a no-op so we don't fight the
/// outer scope's downloaded snapshot.
/// </summary>
public sealed class BlobReadBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IBlobDbCoordinator _coordinator;
    private readonly ILogger<BlobReadBehavior<TRequest, TResponse>> _logger;

    public BlobReadBehavior(
        IBlobDbCoordinator coordinator,
        ILogger<BlobReadBehavior<TRequest, TResponse>> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IAmAuditableCommand)
            return await next();

        if (!_coordinator.IsInsideWriteScope)
        {
            try
            {
                await _coordinator.RefreshFromBlobAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Blob refresh failed for {Request}; serving local copy.", typeof(TRequest).Name);
            }
        }

        return await next();
    }
}
