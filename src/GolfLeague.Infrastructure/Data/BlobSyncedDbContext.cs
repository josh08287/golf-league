using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;

namespace GolfLeague.Infrastructure.Data;

public abstract class BlobSyncedDbContext : DbContext
{
    private readonly BlobContainerClient _containerClient;
    private readonly string _localFilePath;
    private readonly string _blobName;

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
        await UploadToBlobAsync(cancellationToken);
        return result;
    }

    public override int SaveChanges()
    {
        var result = base.SaveChanges();
        UploadToBlobAsync(CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    private async Task UploadToBlobAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_localFilePath))
            return;

        var blobClient = _containerClient.GetBlobClient(_blobName);
        await using var stream = File.OpenRead(_localFilePath);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
    }
}
