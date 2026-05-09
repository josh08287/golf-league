namespace GolfLeague.Domain.Interfaces;

/// <summary>
/// Coordinates the SQLite-in-blob lifecycle. Pipeline behaviors call into
/// this to scope a command's lease/upload around all of its DB work.
/// </summary>
public interface IBlobDbCoordinator
{
    bool IsInsideWriteScope { get; }

    Task RefreshFromBlobAsync(CancellationToken cancellationToken = default);

    Task<IBlobDbWriteScope> BeginWriteAsync(CancellationToken cancellationToken = default);
}

public interface IBlobDbWriteScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
