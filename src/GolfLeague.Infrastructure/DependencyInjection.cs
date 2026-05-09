using Azure.Communication.Email;
using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using GolfLeague.Infrastructure.Email;
using GolfLeague.Infrastructure.Repositories;
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
        var connectionString = configuration["SQL_CONNECTION_STRING"]
            ?? throw new InvalidOperationException("SQL_CONNECTION_STRING is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                // Azure SQL routinely throws transient faults (40197, 40501, 49918, etc.).
                // EnableRetryOnFailure wraps every command in an execution strategy that
                // retries with exponential backoff. MaxRetryCount=6 + base delay 1s gives
                // ~63s total worst-case before surfacing the error to the caller.
                sql.EnableRetryOnFailure(
                    maxRetryCount: 6,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                // Functions Consumption can spend ~30s warming up SQL Serverless from
                // pause. Bump the per-command timeout so cold-start queries don't fail
                // before the DB is ready.
                sql.CommandTimeout(60);
            });

            // Read queries should not pay the change-tracking tax — repositories that
            // need tracking opt in explicitly via .AsTracking().
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IFlightRepository, FlightRepository>();
        services.AddScoped<IRoundRepository, RoundRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IHandicapRepository, HandicapRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IInviteRepository, InviteRepository>();

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

        return services;
    }
}
