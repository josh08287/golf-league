using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Data;

public abstract class BlobSyncedDbContext : DbContext
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _localFilePath;
    private readonly string _blobName;

    // Process-wide lock: only one upload at a time, preventing concurrent
    // requests from racing each other or opening the file mid-write.
    private static readonly SemaphoreSlim _uploadLock = new(1, 1);

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

        if (File.Exists(localFilePath))
        {
            var localInfo = new FileInfo(localFilePath);
            var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (localInfo.LastWriteTimeUtc >= props.Value.LastModified.UtcDateTime)
                return;
        }

        await blobClient.DownloadToAsync(localFilePath, cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        // SQLite releases its write lock once SaveChanges returns, so it is
        // safe to copy and upload the file at this point.
        await UploadToBlobAsync(_containerClient, _localFilePath, _blobName, cancellationToken);
        return result;
    }

    public override int SaveChanges()
    {
        var result = base.SaveChanges();
        UploadToBlobAsync(_containerClient, _localFilePath, _blobName, CancellationToken.None)
            .GetAwaiter().GetResult();
        return result;
    }

    public static Task UploadAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        CancellationToken cancellationToken = default)
        => UploadToBlobAsync(containerClient, localFilePath, blobName, cancellationToken);

    private static async Task UploadToBlobAsync(
        BlobContainerClient containerClient,
        string localFilePath,
        string blobName,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(localFilePath))
            return;

        await _uploadLock.WaitAsync(cancellationToken);
        var tempPath = localFilePath + ".upload-tmp";
        try
        {
            // Copy to a temp file so we hold no lock on the live DB during upload.
            File.Copy(localFilePath, tempPath, overwrite: true);

            var blobClient = containerClient.GetBlobClient(blobName);
            await using var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.None);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
        }
        finally
        {
            _uploadLock.Release();
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
