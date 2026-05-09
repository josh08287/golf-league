using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Data;

public abstract class BlobSyncedDbContext : DbContext
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _localFilePath;
    private readonly string _blobName;

    // Process-wide async lock: serializes all DB transactions in this process so
    // every change is fully persisted to blob storage before the next one starts.
    // Combined with the blob lease below, this also prevents cross-instance races.
    private static readonly SemaphoreSlim _transactionGate = new(1, 1);
    private static readonly AsyncLocal<bool> _syncScopeActive = new();

    // Lease duration must be between 15 and 60 seconds (or -1 for infinite).
    // 60s gives enough headroom for slow uploads without risking long stalls.
    private static readonly TimeSpan _leaseDuration = TimeSpan.FromSeconds(60);

    // Track the ETag of the blob we last synced into our local file. If another
    // Function App instance writes to the blob, its ETag changes and we know to
    // re-download before serving reads. Without this, multi-instance deployments
    // serve stale data because each instance only sees writes that came through
    // it.
    private static readonly ConcurrentDictionary<string, ETag> _knownBlobEtags = new();

    // Process-wide gate for read-side refreshes so two concurrent reads don't
    // both download the same blob. Per-blob would be cleaner but a single gate
    // is fine at this scale.
    private static readonly SemaphoreSlim _readRefreshGate = new(1, 1);

    protected BlobSyncedDbContext(
        DbContextOptions options,
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName)
        : base(options)
    {
        _containerClient = containerClient;
        _localFilePath = localFilePath;
        _blobName = blobName;
    }

    public static async Task DownloadIfNeededAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var blobClient = containerClient.GetBlobClient(blobName);
        var blobExists = await blobClient.ExistsAsync(cancellationToken);

        if (!blobExists.Value)
            return;

        await blobClient.DownloadToAsync(localFilePath, cancellationToken);
        var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        _knownBlobEtags[blobName] = props.Value.ETag;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_syncScopeActive.Value)
            return await base.SaveChangesAsync(cancellationToken);

        if (!ChangeTracker.HasChanges())
            return await base.SaveChangesAsync(cancellationToken);

        // Serialize: only one transaction at a time across the whole process.
        await _transactionGate.WaitAsync(cancellationToken);
        BlobLeaseClient? leaseClient = null;
        string? leaseId = null;
        try
        {
            // Acquire a cross-instance lease so two Function App instances can't
            // race each other.
            (leaseClient, leaseId) = await AcquireLeaseAsync(_containerClient, _blobName, cancellationToken);

            // Refresh local DB from blob before writing so we have the latest state.
            await Database.CloseConnectionAsync();
            await TryDownloadLatestAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);

            // Reload entities from disk so EF doesn't apply stale tracked changes
            // on top of refreshed-from-blob data. (We re-attach the pending changes
            // by letting EF detect them again — see notes below.)
            // In practice, repository methods open a fresh DbContext per request,
            // so the tracked-change set here is just what's queued for THIS save.
            // SQLite's atomic file replace means the connection sees the new file
            // on the next query, but the tracked changes remain pending.

            var result = await base.SaveChangesAsync(cancellationToken);

            await Database.CloseConnectionAsync();
            await UploadToBlobAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);
            return result;
        }
        finally
        {
            if (leaseClient is not null)
            {
                try { await leaseClient.ReleaseAsync(cancellationToken: cancellationToken); }
                catch { /* lease may have expired; nothing to release */ }
            }
            _transactionGate.Release();
        }
    }

    public override int SaveChanges()
        => SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();

    public async Task ExecuteWithBlobSyncAsync(
        Func<Task> operation,
        bool uploadAfter,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithBlobSyncAsync(async () =>
        {
            await operation();
            return true;
        }, uploadAfter, cancellationToken);
    }

    public async Task<T> ExecuteWithBlobSyncAsync<T>(
        Func<Task<T>> operation,
        bool uploadAfter,
        CancellationToken cancellationToken = default)
    {
        if (!uploadAfter)
        {
            // Read path: refresh local file from blob if another instance wrote to
            // it since we last synced. We use the blob's ETag as a cheap "did
            // anything change?" check — GetPropertiesAsync is a HEAD request,
            // not a download — and only download the body when the ETag differs.
            await RefreshIfRemoteChangedAsync(cancellationToken);
            return await operation();
        }

        await _transactionGate.WaitAsync(cancellationToken);
        BlobLeaseClient? leaseClient = null;
        string? leaseId = null;
        var previousSyncScope = _syncScopeActive.Value;

        try
        {
            (leaseClient, leaseId) = await AcquireLeaseAsync(_containerClient, _blobName, cancellationToken);
            await Database.CloseConnectionAsync();
            await TryDownloadLatestAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);

            ChangeTracker.Clear();
            _syncScopeActive.Value = true;

            var result = await operation();

            if (uploadAfter)
            {
                await Database.CloseConnectionAsync();
                await UploadToBlobAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);
            }

            return result;
        }
        finally
        {
            _syncScopeActive.Value = previousSyncScope;
            if (leaseClient is not null)
            {
                try { await leaseClient.ReleaseAsync(cancellationToken: cancellationToken); }
                catch { }
            }
            _transactionGate.Release();
        }
    }

    public static Task UploadAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        CancellationToken cancellationToken = default)
        => UploadToBlobAsync(containerClient, localFilePath, blobName, leaseId: null, cancellationToken);

    private static async Task<(BlobLeaseClient leaseClient, string leaseId)> AcquireLeaseAsync(
        BlobContainerClient containerClient,
        string blobName,
        CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(blobName);

        // Ensure the blob exists so we can lease it. If it doesn't, create an
        // empty placeholder (first-run / fresh environment).
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            using var emptyStream = new MemoryStream();
            await blobClient.UploadAsync(emptyStream, overwrite: false, cancellationToken: cancellationToken);
        }

        var leaseClient = blobClient.GetBlobLeaseClient();

        // Retry briefly if another instance currently holds the lease.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var lease = await leaseClient.AcquireAsync(_leaseDuration, cancellationToken: cancellationToken);
                return (leaseClient, lease.Value.LeaseId);
            }
            catch (RequestFailedException ex) when (ex.Status == 409 && attempt < 30)
            {
                // 409 Conflict = LeaseAlreadyPresent. Wait and retry.
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
    }

    private static async Task DownloadLatestAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        string leaseId,
        CancellationToken cancellationToken)
    {
        var blobClient = containerClient.GetBlobClient(blobName);
        var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

        // Empty placeholder blob — nothing to download.
        if (props.Value.ContentLength == 0)
            return;

        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var conditions = new BlobRequestConditions { LeaseId = leaseId };
        await blobClient.DownloadToAsync(localFilePath, conditions: conditions, transferOptions: default, cancellationToken: cancellationToken);
    }

    private static async Task TryDownloadLatestAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        string leaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            await DownloadLatestAsync(containerClient, localFilePath, blobName, leaseId, cancellationToken);
        }
        catch (IOException) when (File.Exists(localFilePath))
        {
        }
    }

    private static async Task UploadToBlobAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        string? leaseId,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(localFilePath))
            return;

        var tempPath = localFilePath + ".upload-tmp";
        try
        {
            // Copy to a temp file so we hold no lock on the live DB during upload.
            await using (var source = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            await using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            var blobClient = containerClient.GetBlobClient(blobName);
            var conditions = leaseId is null ? null : new BlobRequestConditions { LeaseId = leaseId };
            var uploadOptions = new BlobUploadOptions { Conditions = conditions };

            await using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var response = await blobClient.UploadAsync(stream, uploadOptions, cancellationToken: cancellationToken);
            // Record the new ETag so this instance's next read doesn't redundantly
            // re-download the blob it just wrote.
            _knownBlobEtags[blobName] = response.Value.ETag;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private async Task RefreshIfRemoteChangedAsync(CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(_blobName);

        Response<BlobProperties> propsResponse;
        try
        {
            propsResponse = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob doesn't exist yet — nothing to download.
            return;
        }

        var remoteEtag = propsResponse.Value.ETag;
        if (_knownBlobEtags.TryGetValue(_blobName, out var localEtag) &&
            localEtag == remoteEtag &&
            File.Exists(_localFilePath))
        {
            // Local file matches what's in blob storage — no refresh needed.
            return;
        }

        // Empty placeholder blob — nothing useful to download.
        if (propsResponse.Value.ContentLength == 0)
            return;

        await _readRefreshGate.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the gate; another thread may have refreshed.
            if (_knownBlobEtags.TryGetValue(_blobName, out localEtag) &&
                localEtag == remoteEtag &&
                File.Exists(_localFilePath))
            {
                return;
            }

            await Database.CloseConnectionAsync();

            var directory = Path.GetDirectoryName(_localFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            try
            {
                await blobClient.DownloadToAsync(_localFilePath, cancellationToken);
                _knownBlobEtags[_blobName] = remoteEtag;
            }
            catch (IOException) when (File.Exists(_localFilePath))
            {
                // Another thread/process is touching the local file; skip the
                // refresh and let the caller use whatever's currently on disk.
            }
        }
        finally
        {
            _readRefreshGate.Release();
        }
    }
}
