using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GolfLeague.Infrastructure.Data;

/// <summary>
/// Used by `dotnet ef` at design time (migration scaffolding, SQL script
/// generation). The connection string passed here doesn't need to point at a
/// real database — EF only reads the provider's metadata to generate
/// migrations.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0
            ? args[0]
            : "Server=(localdb)\\MSSQLLocalDB;Database=GolfLeague;Trusted_Connection=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
