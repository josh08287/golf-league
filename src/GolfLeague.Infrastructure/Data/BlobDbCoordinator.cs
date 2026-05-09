using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using GolfLeague.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Data;

/// <summary>
/// Owns the SQLite-in-blob lifecycle: lease acquisition, atomic download
/// from blob, and post-write upload. Replaces the per-repository blob-sync
/// path that did one full lease+download+upload per call. With the
/// coordinator, an entire MediatR command (including all the SaveChanges
/// it triggers) runs under a single lease and emits a single upload.
///
/// Process-wide singleton; thread-safe.
/// </summary>
public sealed class BlobDbCoordinator : IBlobDbCoordinator
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _localFilePath;
    private readonly string _blobName;
    private readonly ILogger<BlobDbCoordinator> _logger;

    // Process-wide read gate: makes sure two concurrent reads don't both
    // overwrite the local file at the same time.
    private static readonly SemaphoreSlim _readGate = new(1, 1);

    // Tracks the ETag of the blob version this process has on disk. When the
    // remote ETag differs we know another instance wrote and we need to refresh.
    private static readonly ConcurrentDictionary<string, ETag> _knownEtags = new();

    // Tracks the depth of nested write scopes per AsyncLocal flow so that any
    // accidental nested BeginWriteAsync call returns a no-op disposable
    // instead of deadlocking on the lease.
    private static readonly AsyncLocal<int> _writeScopeDepth = new();

    private static readonly TimeSpan _leaseDuration = TimeSpan.FromSeconds(60);

    public BlobDbCoordinator(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        ILogger<BlobDbCoordinator> logger)
    {
        _containerClient = containerClient;
        _localFilePath = localFilePath;
        _blobName = blobName;
        _logger = logger;
    }

    public string LocalFilePath => _localFilePath;
    public string BlobName => _blobName;
    public bool IsInsideWriteScope => _writeScopeDepth.Value > 0;

    /// <summary>
    /// Refresh the local file from blob storage if the remote ETag differs.
    /// Read-side: takes no lease. The download is atomic: writes to a temp
    /// file then renames, so a crash mid-download cannot corrupt the live
    /// file.
    /// </summary>
    public async Task RefreshFromBlobAsync(CancellationToken cancellationToken = default)
    {
        // If we're already inside a write scope, the local file is current
        // (we downloaded it under the lease) and we must not concurrently
        // download from the read path.
        if (IsInsideWriteScope) return;

        var blobClient = _containerClient.GetBlobClient(_blobName);

        Response<BlobProperties> propsResponse;
        try
        {
            propsResponse = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return;
        }

        if (propsResponse.Value.ContentLength == 0)
            return;

        var remoteEtag = propsResponse.Value.ETag;
        if (_knownEtags.TryGetValue(_blobName, out var cachedEtag) &&
            cachedEtag == remoteEtag &&
            File.Exists(_localFilePath))
        {
            return;
        }

        await _readGate.WaitAsync(cancellationToken);
        try
        {
            // Re-check after grabbing the gate.
            if (_knownEtags.TryGetValue(_blobName, out cachedEtag) &&
                cachedEtag == remoteEtag &&
                File.Exists(_localFilePath))
            {
                return;
            }

            await DownloadAtomicAsync(blobClient, leaseId: null, cancellationToken);
            _knownEtags[_blobName] = remoteEtag;
        }
        finally
        {
            _readGate.Release();
        }
    }

    /// <summary>
    /// Begin a cross-instance exclusive write scope. The returned scope
    /// downloads the latest blob (if remote ETag changed), holds a blob
    /// lease for its lifetime, and uploads the local file on
    /// <see cref="WriteScope.CommitAsync"/>. Disposing without a successful
    /// commit releases the lease without uploading.
    /// </summary>
    async Task<IBlobDbWriteScope> IBlobDbCoordinator.BeginWriteAsync(CancellationToken cancellationToken)
        => await BeginWriteAsync(cancellationToken);

    public async Task<WriteScope> BeginWriteAsync(CancellationToken cancellationToken = default)
    {
        // Nested scopes share the outer scope's lease.
        if (IsInsideWriteScope)
        {
            _writeScopeDepth.Value++;
            return new WriteScope(this, leaseClient: null, leaseId: null, isNested: true);
        }

        var blobClient = _containerClient.GetBlobClient(_blobName);
        await EnsureBlobExistsAsync(blobClient, cancellationToken);

        var leaseClient = blobClient.GetBlobLeaseClient();
        string leaseId = await AcquireLeaseAsync(leaseClient, cancellationToken);

        // Refresh local file from blob if the remote ETag changed since we
        // last synced. We hold the lease so no one else can write between
        // the ETag check and our upload.
        try
        {
            var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var remoteEtag = props.Value.ETag;

            var needRefresh = !_knownEtags.TryGetValue(_blobName, out var cachedEtag) ||
                              cachedEtag != remoteEtag ||
                              !File.Exists(_localFilePath);

            if (needRefresh && props.Value.ContentLength > 0)
            {
                await DownloadAtomicAsync(blobClient, leaseId, cancellationToken);
                _knownEtags[_blobName] = remoteEtag;
            }
        }
        catch
        {
            // Always release the lease on any failure during setup.
            try { await leaseClient.ReleaseAsync(cancellationToken: cancellationToken); } catch { }
            throw;
        }

        _writeScopeDepth.Value++;
        return new WriteScope(this, leaseClient, leaseId, isNested: false);
    }

    private static async Task<string> AcquireLeaseAsync(
        BlobLeaseClient leaseClient,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var lease = await leaseClient.AcquireAsync(_leaseDuration, cancellationToken: cancellationToken);
                return lease.Value.LeaseId;
            }
            catch (RequestFailedException ex) when (ex.Status == 409 && attempt < 60)
            {
                // Another instance holds the lease. Retry up to 30 seconds.
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
    }

    private static async Task EnsureBlobExistsAsync(
        BlobClient blobClient,
        CancellationToken cancellationToken)
    {
        if (await blobClient.ExistsAsync(cancellationToken)) return;
        using var emptyStream = new MemoryStream();
        await blobClient.UploadAsync(emptyStream, overwrite: false, cancellationToken: cancellationToken);
    }

    private async Task DownloadAtomicAsync(
        BlobClient blobClient,
        string? leaseId,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_localFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = _localFilePath + ".download-tmp";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        try
        {
            var conditions = leaseId is null ? null : new BlobRequestConditions { LeaseId = leaseId };
            await blobClient.DownloadToAsync(
                tempPath,
                conditions: conditions,
                transferOptions: default,
                cancellationToken: cancellationToken);

            // Atomic rename so a crash mid-download cannot corrupt the live file.
            File.Move(tempPath, _localFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    /// <summary>
    /// Upload the current local file to blob storage under the given lease.
    /// Records the resulting ETag so subsequent reads on this instance
    /// don't redundantly re-download.
    /// </summary>
    internal async Task UploadAsync(string? leaseId, CancellationToken cancellationToken)
    {
        if (!File.Exists(_localFilePath))
        {
            _logger.LogWarning("Skipping upload: local file {Path} not found.", _localFilePath);
            return;
        }

        var tempPath = _localFilePath + ".upload-tmp";
        try
        {
            // Snapshot the live file so we don't hold its handle while uploading.
            await using (var source = new FileStream(_localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            await using (var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(dest, cancellationToken);
            }

            var blobClient = _containerClient.GetBlobClient(_blobName);
            var conditions = leaseId is null ? null : new BlobRequestConditions { LeaseId = leaseId };
            var options = new BlobUploadOptions { Conditions = conditions };

            await using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var response = await blobClient.UploadAsync(stream, options, cancellationToken: cancellationToken);
            _knownEtags[_blobName] = response.Value.ETag;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    public sealed class WriteScope : IBlobDbWriteScope
    {
        private readonly BlobDbCoordinator _coordinator;
        private readonly BlobLeaseClient? _leaseClient;
        private readonly string? _leaseId;
        private readonly bool _isNested;
        private bool _committed;
        private bool _disposed;

        internal WriteScope(
            BlobDbCoordinator coordinator,
            BlobLeaseClient? leaseClient,
            string? leaseId,
            bool isNested)
        {
            _coordinator = coordinator;
            _leaseClient = leaseClient;
            _leaseId = leaseId;
            _isNested = isNested;
        }

        public string? LeaseId => _leaseId;

        /// <summary>
        /// Upload the local file to blob under the held lease. Must be called
        /// before <see cref="DisposeAsync"/> to actually persist changes; if
        /// not called, the scope releases its lease without uploading
        /// (treated as rollback).
        /// </summary>
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_isNested)
            {
                // Nested writes commit when the outermost scope commits.
                _committed = true;
                return;
            }

            if (_committed) return;
            await _coordinator.UploadAsync(_leaseId, cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _writeScopeDepth.Value = Math.Max(0, _writeScopeDepth.Value - 1);

            if (_isNested) return;

            if (_leaseClient is not null)
            {
                try { await _leaseClient.ReleaseAsync(); } catch { /* lease may have expired */ }
            }
        }
    }
}
