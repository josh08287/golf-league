using System.Text;
using GolfLeague.Application.Behaviors;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Enums;
using GolfLeague.Functions.Middleware;
using GolfLeague.Infrastructure;
using GolfLeague.Infrastructure.Auth;
using GolfLeague.Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(workerApp =>
    {
        workerApp.UseMiddleware<AuthMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        var signingKey = config["JWT_SIGNING_KEY"]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY is not configured.");

        if (signingKey.Length < 32)
            throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 characters.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtTokenService.Issuer,
                    ValidateAudience = true,
                    ValidAudience = JwtTokenService.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = "role",
                    NameClaimType = ClaimTypes.NameIdentifier,
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
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });
    })
    .Build();

await EnsureDatabaseInitializedAsync(host);

await host.RunAsync();

static async Task EnsureDatabaseInitializedAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Startup: applying EF Core migrations.");

    // MigrateAsync is idempotent — it only runs migrations the database is
    // missing. The DbContext's configured execution strategy handles
    // transient Azure SQL faults during the initial connection. We
    // deliberately let exceptions propagate so the host fails to start on
    // permission / config errors instead of running with an empty schema
    // and 500ing every request.
    var strategy = dbContext.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await dbContext.Database.MigrateAsync();
    });

    logger.LogInformation("Startup: migrations applied. Seeding active season if missing.");
    await SeedActiveSeasonAsync(dbContext);
    await BootstrapAdminAsync(scope.ServiceProvider, logger);
    logger.LogInformation("Startup: seed complete.");
}

static async Task SeedActiveSeasonAsync(AppDbContext dbContext)
{
    var hasActiveSeason = await dbContext.Seasons.AnyAsync(s => s.IsActive);
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

static async Task BootstrapAdminAsync(IServiceProvider services, ILogger logger)
{
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var dbContext = services.GetRequiredService<AppDbContext>();
    var config = services.GetRequiredService<IConfiguration>();

    var bootstrapEmail = config["ADMIN_BOOTSTRAP_EMAIL"];
    if (string.IsNullOrWhiteSpace(bootstrapEmail))
    {
        logger.LogInformation("Startup: ADMIN_BOOTSTRAP_EMAIL not set; skipping admin bootstrap.");
        return;
    }

    var anyAdmin = await dbContext.Users.AnyAsync(u => u.Role == PlayerRole.Admin);
    if (anyAdmin)
    {
        logger.LogInformation("Startup: admin user already exists; skipping bootstrap.");
        return;
    }

    var existing = await userManager.FindByEmailAsync(bootstrapEmail);
    if (existing is not null)
    {
        // User exists but isn't admin yet — promote and require MFA enrollment.
        existing.Role = PlayerRole.Admin;
        await userManager.UpdateAsync(existing);
        logger.LogWarning(
            "Startup: promoted existing user {Email} to admin. They must set a password and enroll MFA.",
            bootstrapEmail);
        return;
    }

    var user = new AppUser
    {
        UserName = bootstrapEmail,
        Email = bootstrapEmail,
        EmailConfirmed = false,
        Role = PlayerRole.Admin,
        CreatedAt = DateTime.UtcNow,
    };

    // Created with no password — admin must use the "forgot password"
    // flow on first login to set one. This avoids ever holding a
    // bootstrap secret in plaintext config.
    var result = await userManager.CreateAsync(user);
    if (!result.Succeeded)
    {
        var errors = string.Join("; ", result.Errors.Select(e => e.Description));
        logger.LogError("Startup: failed to create bootstrap admin: {Errors}", errors);
        return;
    }

    logger.LogWarning(
        "Startup: created bootstrap admin {Email} (no password set). " +
        "Use the password-reset flow to complete account setup.",
        bootstrapEmail);
}
