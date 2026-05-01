using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GolfLeague.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var dbPath = args.Length > 0 ? args[0] : "golf-league.db";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        // Stub container client — not used during migrations/design-time operations
        var stubUri = new Uri("https://stub.blob.core.windows.net/stub");
        var containerClient = new BlobContainerClient(stubUri);

        return new AppDbContext(options, containerClient, dbPath, Path.GetFileName(dbPath));
    }
}
