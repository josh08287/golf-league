using Azure.Storage.Blobs;
using GolfLeague.Application.Behaviors;
using GolfLeague.Functions.Middleware;
using GolfLeague.Infrastructure;
using GolfLeague.Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerApp =>
    {
        workerApp.UseMiddleware<AuthMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        var tenantId = config["ENTRA_TENANT_ID"]
            ?? throw new InvalidOperationException("ENTRA_TENANT_ID is not configured.");
        var clientId = config["ENTRA_CLIENT_ID"]
            ?? throw new InvalidOperationException("ENTRA_CLIENT_ID is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                options.Audience = clientId;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireRole("admin"));

            options.AddPolicy("ScorerOrAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx =>
                          ctx.User.IsInRole("admin") || ctx.User.IsInRole("scorer")));

            options.AddPolicy("Authenticated", policy =>
                policy.RequireAuthenticatedUser());
        });

        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services.AddInfrastructure(config);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(GolfLeague.Application.Players.Commands.CreatePlayerCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });

    })
    .Build();

await EnsureDatabaseInitializedAsync(host);

await host.RunAsync();

static async Task EnsureDatabaseInitializedAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var containerClient = scope.ServiceProvider.GetRequiredService<BlobContainerClient>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var blobName = config["SQLITE_BLOB_NAME"] ?? "golf-league-v2.db";
    var localDbPath = Path.Combine(Path.GetTempPath(), "golf-league", blobName);

    try
    {
        await BlobSyncedDbContext.DownloadIfNeededAsync(containerClient, localDbPath, blobName);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The two-half refactor changed the schema (Rounds.WeekNumber, gross/net
        // Stableford columns, etc.). EnsureCreated does not migrate, so if the
        // downloaded blob is the old (v1) schema we drop and recreate.
        if (await IsLegacySchemaAsync(dbContext))
        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Detected legacy v1 schema in {Blob}; dropping and recreating.", blobName);
            await dbContext.Database.EnsureDeletedAsync();
        }

        // No EF migrations — v2 schema is created fresh from the model.
        await dbContext.Database.EnsureCreatedAsync();

        // Seed a default active season if none exists so flights can be created.
        await SeedActiveSeasonAsync(dbContext);

        // Upload the schema-updated DB back to blob storage.
        await BlobSyncedDbContext.UploadAsync(containerClient, localDbPath, blobName);
    }
    catch (Exception ex)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization failed; the host will still start but requests may fail.");
    }
}

static async Task<bool> IsLegacySchemaAsync(AppDbContext dbContext)
{
    // EnsureCreated only creates tables when none exist. If the DB was downloaded
    // from blob storage and predates the two-half refactor, the Rounds table will
    // be missing the WeekNumber column we now require. Probing for it tells us
    // whether to drop & recreate.
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='Rounds';";
            var hasRoundsTable = await cmd.ExecuteScalarAsync() is not null;
            if (!hasRoundsTable) return false; // empty DB — let EnsureCreated handle it

            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Rounds') WHERE name='WeekNumber';";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count == 0;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
    catch
    {
        // If the probe itself fails, assume the file is corrupt/legacy and rebuild.
        return true;
    }
}

static async Task SeedActiveSeasonAsync(AppDbContext dbContext)
{
    var hasActiveSeason = dbContext.Seasons.Any(s => s.IsActive);
    if (hasActiveSeason) return;

    var year = DateTime.UtcNow.Year;
    var start = new DateOnly(year, 5, 1);
    var end = new DateOnly(year, 9, 30);
    var midpoint = start.AddDays((end.DayNumber - start.DayNumber) / 2);

    var season = new GolfLeague.Domain.Entities.Season
    {
        Name = $"{year} Season",
        Year = year,
        StartDate = start,
        EndDate = end,
        IsActive = true,
    };
    dbContext.Seasons.Add(season);
    await dbContext.SaveChangesAsync();

    dbContext.SeasonHalves.AddRange(
        new GolfLeague.Domain.Entities.SeasonHalf
        {
            SeasonId = season.Id,
            HalfNumber = 1,
            Name = $"{season.Name} - First Half",
            StartDate = start,
            EndDate = midpoint,
            CreatedAt = DateTime.UtcNow,
        },
        new GolfLeague.Domain.Entities.SeasonHalf
        {
            SeasonId = season.Id,
            HalfNumber = 2,
            Name = $"{season.Name} - Second Half",
            StartDate = midpoint.AddDays(1),
            EndDate = end,
            CreatedAt = DateTime.UtcNow,
        });
    await dbContext.SaveChangesAsync();
}
