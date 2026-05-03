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

    var blobName = config["SQLITE_BLOB_NAME"] ?? "golf-league.db";
    var localDbPath = Path.Combine(Path.GetTempPath(), "golf-league", blobName);

    try
    {
        await BlobSyncedDbContext.DownloadIfNeededAsync(containerClient, localDbPath, blobName);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Apply any pending EF migrations (schema changes)
        await dbContext.Database.MigrateAsync();

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

static async Task SeedActiveSeasonAsync(AppDbContext dbContext)
{
    var hasActiveSeason = dbContext.Seasons.Any(s => s.IsActive);
    if (hasActiveSeason) return;

    var year = DateTime.UtcNow.Year;
    dbContext.Seasons.Add(new GolfLeague.Domain.Entities.Season
    {
        Name = $"{year} Season",
        Year = year,
        StartDate = new DateOnly(year, 1, 1),
        EndDate = new DateOnly(year, 12, 31),
        IsActive = true,
    });
    await dbContext.SaveChangesAsync();
}
