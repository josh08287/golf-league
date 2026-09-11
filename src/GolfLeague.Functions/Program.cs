using System.Text;
using GolfLeague.Application.Behaviors;
using GolfLeague.Functions.Middleware;
using GolfLeague.Infrastructure;
using GolfLeague.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
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
        workerApp.UseMiddleware<ExceptionHandlingMiddleware>();
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

// On a Consumption plan the host recycles/scales-to-zero far more often than real deploys
// happen, and each cold start here would otherwise wake the SQL Serverless DB from auto-pause.
// Default is off in Azure; the deploy workflow calls POST /admin/migrate once per deploy instead.
// Local dev sets RUN_MIGRATIONS_ON_STARTUP=true in local.settings.json for convenience.
var runMigrationsOnStartup = host.Services.GetRequiredService<IConfiguration>()
    .GetValue<bool>("RUN_MIGRATIONS_ON_STARTUP");

if (runMigrationsOnStartup)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    try
    {
        await GolfLeague.Functions.DatabaseInitializer.EnsureDatabaseInitializedAsync(host.Services, logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex,
            "Startup: database initialization failed — the host will start but DB may be unavailable. " +
            "Azure SQL Serverless may still be resuming from auto-pause; the next host recycle will retry.");
    }
}

await host.RunAsync();
