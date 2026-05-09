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

    try
    {
        // Take the cross-instance write lease so we don't race another worker
        // starting up at the same time. BeginWriteAsync also pulls the latest
        // blob into the local file (atomically — temp file + rename).
        await using var writeScope = await coordinator.BeginWriteAsync();

        await dbContext.Database.EnsureCreatedAsync();
        await SeedActiveSeasonAsync(dbContext);

        // Commit uploads the local file under the same lease and records the
        // resulting ETag so this instance's later reads don't redundantly
        // re-download.
        await writeScope.CommitAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed; the host will still start but requests may fail.");
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
