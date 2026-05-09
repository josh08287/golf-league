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
                    ValidateIssuerSigningKey = true,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAuthenticatedUser().RequireRole("admin"));

            options.AddPolicy("ScorerOrAdmin", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => ctx.User.IsInRole("admin") || ctx.User.IsInRole("scorer")));

            options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
        });

        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services.AddInfrastructure(config);

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(GolfLeague.Application.Players.Commands.CreatePlayerCommand).Assembly);
            // Order matters: write scope must be the outermost behavior so the
            // lease and transaction wrap everything (including audit logging).
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(BlobWriteBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(BlobReadBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });
    })
    .Build();

await EnsureDatabaseInitializedAsync(host);

await host.RunAsync();

static async Task EnsureDatabaseInitializedAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var coordinator = scope.ServiceProvider.GetRequiredService<BlobDbCoordinator>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Startup: initializing database. Local path={Path}", coordinator.LocalFilePath);

    try
    {
        // Take the cross-instance write lease so we don't race another worker
        // starting up at the same time. BeginWriteAsync also pulls the latest
        // blob into the local file (atomically — temp file + rename).
        logger.LogInformation("Startup: BeginWriteAsync — acquiring blob lease and pulling latest snapshot.");
        await using var writeScope = await coordinator.BeginWriteAsync();

        var localFileExisted = File.Exists(coordinator.LocalFilePath);
        var localFileSize = localFileExisted ? new FileInfo(coordinator.LocalFilePath).Length : 0;
        logger.LogInformation(
            "Startup: lease acquired. Local file exists={Exists}, size={Size} bytes.",
            localFileExisted, localFileSize);

        // EnsureCreatedAsync is a no-op if the schema already exists. If the
        // blob was empty / placeholder this creates the schema fresh.
        var created = await dbContext.Database.EnsureCreatedAsync();
        logger.LogInformation("Startup: EnsureCreatedAsync returned created={Created}.", created);

        // Defensive check: if for any reason the Rounds table is missing
        // (corrupt / truncated blob), force a clean recreate. The legacy
        // probe used to do this; keeping it here as a safety net only.
        if (!await TableExistsAsync(dbContext, "Rounds"))
        {
            logger.LogWarning("Startup: Rounds table missing after EnsureCreated. Forcing EnsureDeleted + EnsureCreated.");
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }

        await SeedActiveSeasonAsync(dbContext);
        logger.LogInformation("Startup: seed complete.");

        // Commit uploads the local file under the same lease and records the
        // resulting ETag so this instance's later reads don't redundantly
        // re-download.
        await writeScope.CommitAsync();
        logger.LogInformation("Startup: commit complete — blob uploaded under lease.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startup: database initialization failed; the host will still start but requests may fail.");
    }
}

static async Task<bool> TableExistsAsync(AppDbContext dbContext, string tableName)
{
    var connection = dbContext.Database.GetDbConnection();
    var opened = false;
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
        opened = true;
    }
    try
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result is not null;
    }
    finally
    {
        if (opened) await connection.CloseAsync();
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
