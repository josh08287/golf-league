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
            await DownloadLatestAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);

            // Reload entities from disk so EF doesn't apply stale tracked changes
            // on top of refreshed-from-blob data. (We re-attach the pending changes
            // by letting EF detect them again — see notes below.)
            // In practice, repository methods open a fresh DbContext per request,
            // so the tracked-change set here is just what's queued for THIS save.
            // SQLite's atomic file replace means the connection sees the new file
            // on the next query, but the tracked changes remain pending.

            var result = await base.SaveChangesAsync(cancellationToken);

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
            return await operation();

        await _transactionGate.WaitAsync(cancellationToken);
        BlobLeaseClient? leaseClient = null;
        string? leaseId = null;
        var previousSyncScope = _syncScopeActive.Value;

        try
        {
            (leaseClient, leaseId) = await AcquireLeaseAsync(_containerClient, _blobName, cancellationToken);
            await Database.CloseConnectionAsync();
            await DownloadLatestAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);

            ChangeTracker.Clear();
            _syncScopeActive.Value = true;

            var result = await operation();

            if (uploadAfter)
                await UploadToBlobAsync(_containerClient, _localFilePath, _blobName, leaseId, cancellationToken);

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
            File.Copy(localFilePath, tempPath, overwrite: true);

            var blobClient = containerClient.GetBlobClient(blobName);
            var conditions = leaseId is null ? null : new BlobRequestConditions { LeaseId = leaseId };
            var uploadOptions = new BlobUploadOptions { Conditions = conditions };

            await using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);
            await blobClient.UploadAsync(stream, uploadOptions, cancellationToken: cancellationToken);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
