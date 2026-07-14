using System.Text;
using GolfLeague.Application.Behaviors;
using GolfLeague.Domain.Entities;
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
        workerApp.UseMiddleware<LeagueContextMiddleware>();
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

try
{
    await EnsureDatabaseInitializedAsync(host);
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex,
        "Startup: database initialization failed — the host will start but DB may be unavailable. " +
        "Azure SQL Serverless may still be resuming from auto-pause; the next host recycle will retry.");
}

await host.RunAsync();

static async Task EnsureDatabaseInitializedAsync(IHost host)
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Startup: applying EF Core migrations.");

    // EnableRetryOnFailure handles transient faults per-command, but MigrateAsync
    // opens its own connection before the strategy fires. Wrap the whole call so
    // a 42119 (server busy / serverless resume) at connection-open time is retried.
    var strategy = dbContext.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await dbContext.Database.MigrateAsync();
    });

    logger.LogInformation("Startup: migrations applied. Seeding roles + default league + active season if missing.");
    await SeedRolesAsync(scope.ServiceProvider, logger);
    var defaultLeague = await SeedDefaultLeagueAsync(dbContext, scope.ServiceProvider.GetRequiredService<IConfiguration>());
    await SeedActiveSeasonAsync(dbContext, defaultLeague.Id);
    await BootstrapAdminAsync(scope.ServiceProvider, logger);
    await BootstrapSuperAdminAsync(scope.ServiceProvider, logger);
    logger.LogInformation("Startup: seed complete.");
}

static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var name in new[] { "admin", "scorer", "player" })
    {
        if (!await roleManager.RoleExistsAsync(name))
        {
            var result = await roleManager.CreateAsync(new IdentityRole<Guid> { Name = name });
            if (!result.Succeeded)
            {
                logger.LogError(
                    "Startup: failed to seed role {Role}: {Errors}",
                    name, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

static async Task<GolfLeague.Domain.Entities.League> SeedDefaultLeagueAsync(AppDbContext dbContext, IConfiguration config)
{
    var defaultSlug = config["DEFAULT_LEAGUE_SLUG"] ?? "capital";
    var existing = await dbContext.Leagues.FirstOrDefaultAsync(l => l.Slug == defaultSlug);
    if (existing is not null) return existing;

    var league = new GolfLeague.Domain.Entities.League
    {
        Name = config["DEFAULT_LEAGUE_NAME"] ?? "Capital Golf League",
        Slug = defaultSlug,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };
    dbContext.Leagues.Add(league);
    await dbContext.SaveChangesAsync();
    return league;
}

static async Task SeedActiveSeasonAsync(AppDbContext dbContext, int leagueId)
{
    var hasActiveSeason = await dbContext.Seasons.AnyAsync(s => s.IsActive && s.LeagueId == leagueId);
    if (hasActiveSeason) return;

    var year = DateTime.UtcNow.Year;
    var start = new DateOnly(year, 5, 1);
    var end = new DateOnly(year, 9, 30);
    var midpoint = start.AddDays((end.DayNumber - start.DayNumber) / 2);

    var season = new GolfLeague.Domain.Entities.Season
    {
        LeagueId = leagueId,
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

    if (bootstrapEmail.StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase))
    {
        logger.LogError(
            "Startup: ADMIN_BOOTSTRAP_EMAIL resolved to a raw Key Vault reference string — " +
            "the secret is missing or the managed identity lacks access. " +
            "Create the 'AdminBootstrapEmail' secret in Key Vault and restart the Function App.");
        return;
    }

    // Check whether anyone already holds the admin role.
    var anyAdmin = await dbContext.UserRoles
        .AnyAsync(ur => dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == "admin"));
    if (anyAdmin)
    {
        logger.LogInformation("Startup: admin user already exists; skipping bootstrap.");
        return;
    }

    var existing = await userManager.FindByEmailAsync(bootstrapEmail);
    if (existing is not null)
    {
        if (!await userManager.IsInRoleAsync(existing, "admin"))
        {
            var addResult = await userManager.AddToRoleAsync(existing, "admin");
            if (!addResult.Succeeded)
            {
                logger.LogError(
                    "Startup: failed to grant admin role to existing user {Email}: {Errors}",
                    bootstrapEmail, string.Join("; ", addResult.Errors.Select(e => e.Description)));
                return;
            }
        }
        logger.LogWarning(
            "Startup: granted admin role to existing user {Email}. They must enroll MFA on next sign-in.",
            bootstrapEmail);
        return;
    }

    var user = new AppUser
    {
        UserName = bootstrapEmail,
        Email = bootstrapEmail,
        EmailConfirmed = false,
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

    var roleResult = await userManager.AddToRoleAsync(user, "admin");
    if (!roleResult.Succeeded)
    {
        logger.LogError(
            "Startup: created bootstrap admin {Email} but failed to assign admin role: {Errors}",
            bootstrapEmail, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        return;
    }

    logger.LogWarning(
        "Startup: created bootstrap admin {Email} (no password set). " +
        "Use the password-reset flow to complete account setup.",
        bootstrapEmail);
}

static async Task BootstrapSuperAdminAsync(IServiceProvider services, ILogger logger)
{
    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    const string superAdminEmail = "josh.b.blaine@gmail.com";

    var user = await userManager.FindByEmailAsync(superAdminEmail);
    if (user is null)
    {
        logger.LogInformation("Startup: super-admin account {Email} not found; skipping.", superAdminEmail);
        return;
    }

    if (user.IsSuperAdmin)
    {
        logger.LogInformation("Startup: {Email} is already super-admin.", superAdminEmail);
        return;
    }

    user.IsSuperAdmin = true;
    var result = await userManager.UpdateAsync(user);
    if (result.Succeeded)
        logger.LogWarning("Startup: granted IsSuperAdmin to {Email}.", superAdminEmail);
    else
        logger.LogError(
            "Startup: failed to set IsSuperAdmin on {Email}: {Errors}",
            superAdminEmail, string.Join("; ", result.Errors.Select(e => e.Description)));
}
