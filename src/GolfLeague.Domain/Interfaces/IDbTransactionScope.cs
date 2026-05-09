namespace GolfLeague.Domain.Interfaces;

/// <summary>
/// Wraps an underlying database transaction so the application layer can
/// commit or roll back without referencing EF Core directly.
/// </summary>
public interface IDbTransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface IDbTransactionFactory
{
    Task<IDbTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
