using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Behaviors;

/// <summary>
/// For every <see cref="IAmAuditableCommand"/>: acquire the blob lease,
/// open a SQLite transaction, run the handler, then either commit
/// (transaction + upload) or roll back (no upload). One blob lease + one
/// upload per command, regardless of how many <c>SaveChanges</c> calls
/// the handler chains together.
/// </summary>
public sealed class BlobWriteBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IBlobDbCoordinator _coordinator;
    private readonly IDbTransactionFactory _transactions;
    private readonly ILogger<BlobWriteBehavior<TRequest, TResponse>> _logger;

    public BlobWriteBehavior(
        IBlobDbCoordinator coordinator,
        IDbTransactionFactory transactions,
        ILogger<BlobWriteBehavior<TRequest, TResponse>> logger)
    {
        _coordinator = coordinator;
        _transactions = transactions;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAmAuditableCommand)
            return await next();

        // Acquire the blob lease (cross-instance exclusion). This also pulls
        // any newer remote snapshot before we start mutating.
        await using var writeScope = await _coordinator.BeginWriteAsync(cancellationToken);

        // Wrap the handler in a SQLite transaction so partial work is rolled
        // back on failure and never uploaded to blob.
        await using var transaction = await _transactions.BeginTransactionAsync(cancellationToken);

        TResponse response;
        try
        {
            response = await next();
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); } catch { }
            throw;
        }

        // Roll back when the handler returned a failed Result so we don't
        // upload half-applied changes for a logically-failed command.
        if (!IsSuccessResult(response))
        {
            try { await transaction.RollbackAsync(cancellationToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "Rollback after failed result raised an exception."); }
            return response;
        }

        await transaction.CommitAsync(cancellationToken);
        await writeScope.CommitAsync(cancellationToken);
        return response;
    }

    private static bool IsSuccessResult(TResponse response)
    {
        if (response is null) return false;

        var type = typeof(TResponse);
        if (!type.IsGenericType) return true;

        var prop = type.GetProperty(nameof(Result<object>.IsSuccess));
        return prop?.GetValue(response) is true;
    }
}
