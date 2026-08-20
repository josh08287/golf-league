using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Communication.Email;
using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Auth;
using GolfLeague.Infrastructure.Data;
using GolfLeague.Infrastructure.Email;
using GolfLeague.Infrastructure.Repositories;
using GolfLeague.Infrastructure.ScorecardOcr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rawConnectionString = configuration["SQL_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("SQL_CONNECTION_STRING is not configured.");

        // Cap the ADO.NET pool explicitly. With EnableRetryOnFailure below (10 retries,
        // up to 60s delay each) a burst of transient Azure SQL faults — e.g. serverless
        // auto-pause resume — can otherwise pin the default 100-connection pool for
        // minutes, making the app look hung to new requests until connections free up.
        var connectionStringBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(rawConnectionString)
        {
            MaxPoolSize = 50,
        };
        var connectionString = connectionStringBuilder.ConnectionString;

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // Azure SQL routinely throws transient faults (40197, 40501, 49918, etc.).
                // Serverless auto-pause resume (42119) can take 60-90s; use 10 retries
                // with a 60s max delay so the total wait covers a full cold-start.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 10,
                    maxRetryDelay: TimeSpan.FromSeconds(60),
                    // 42119: server busy / database resuming from serverless auto-pause
                    errorNumbersToAdd: [42119]);

                // Allow up to 120s for a single command so cold-start queries don't
                // time out before the DB finishes resuming.
                sql.CommandTimeout(120);
            });

            // Read queries should not pay the change-tracking tax — repositories that
            // need tracking opt in explicitly via .AsTracking().
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // ASP.NET Core Identity — backs the AppUser table, password hashing,
        // external login linking, lockout, email confirmation tokens, etc.
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
                options.Lockout.MaxFailedAccessAttempts = 8;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IFlightRepository, FlightRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<ITeeTimeRepository, TeeTimeRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IHandicapRepository, HandicapRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<ILeagueSettingRepository, LeagueSettingRepository>();
        services.AddScoped<IPlayerHalfSettingRepository, PlayerHalfSettingRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<ITeeTimeService, TeeTimeService>();
        services.AddScoped<ITeeTimeAutofillService, TeeTimeAutofillService>();
        services.AddScoped<TournamentFoursomeService>();

        services.AddScoped<LeagueContext>();
        services.AddScoped<ILeagueContext>(sp => sp.GetRequiredService<LeagueContext>());

        services.AddMemoryCache();
        services.AddHttpClient("google");
        services.AddHttpClient("facebook");

        services.AddFido2(options =>
        {
            options.ServerDomain = configuration["FIDO2_RP_ID"] ?? "localhost";
            options.ServerName = configuration["FIDO2_RP_NAME"] ?? "Capital Golf League";
            options.Origins = new HashSet<string>(
                (configuration["FIDO2_RP_ORIGINS"] ?? "http://localhost:5173")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            options.TimestampDriftTolerance = 300_000;
        });

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IExternalAuthService, ExternalAuthService>();
        services.AddScoped<IPasskeyService, PasskeyService>();

        var acsConnectionString = configuration["ACS_CONNECTION_STRING"];
        var acsSenderAddress = configuration["ACS_SENDER_ADDRESS"];
        if (!string.IsNullOrWhiteSpace(acsConnectionString) && !string.IsNullOrWhiteSpace(acsSenderAddress))
        {
            var emailClient = new EmailClient(acsConnectionString);
            services.AddSingleton(sp => new AzureCommunicationEmailService(
                emailClient,
                acsSenderAddress,
                sp.GetRequiredService<ILogger<AzureCommunicationEmailService>>()));
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<AzureCommunicationEmailService>());
        }
        else
        {
            services.AddSingleton<IEmailService, NoOpEmailService>();
        }

        var documentIntelligenceEndpoint = configuration["DOCUMENT_INTELLIGENCE_ENDPOINT"];
        var documentIntelligenceKey = configuration["DOCUMENT_INTELLIGENCE_KEY"];

        // An unresolved Key Vault reference comes through as the literal
        // "@Microsoft.KeyVault(...)" string, not a blank value — a plain
        // null/whitespace check would treat that as a real key and try to
        // authenticate Document Intelligence with it. Same failure mode
        // Program.cs already guards against for ADMIN_BOOTSTRAP_EMAIL.
        var documentIntelligenceKeyUnresolved = documentIntelligenceKey?
            .StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase) ?? false;

        if (!string.IsNullOrWhiteSpace(documentIntelligenceEndpoint)
            && !string.IsNullOrWhiteSpace(documentIntelligenceKey)
            && !documentIntelligenceKeyUnresolved)
        {
            var diClient = new DocumentIntelligenceClient(
                new Uri(documentIntelligenceEndpoint),
                new AzureKeyCredential(documentIntelligenceKey));
            services.AddSingleton(sp => new DocumentIntelligenceScorecardOcrService(
                diClient,
                sp.GetRequiredService<ILogger<DocumentIntelligenceScorecardOcrService>>()));
            services.AddSingleton<IScorecardOcrService>(sp => sp.GetRequiredService<DocumentIntelligenceScorecardOcrService>());
        }
        else
        {
            if (documentIntelligenceKeyUnresolved)
            {
                Console.Error.WriteLine(
                    "Startup: DOCUMENT_INTELLIGENCE_KEY resolved to a raw Key Vault reference string — " +
                    "the secret is missing or the managed identity lacks access. Scorecard OCR will stay " +
                    "disabled until the 'DocumentIntelligenceKey' secret is created in Key Vault and the " +
                    "Function App is restarted.");
            }

            services.AddSingleton<IScorecardOcrService, NoOpScorecardOcrService>();
        }

        return services;
    }
}
