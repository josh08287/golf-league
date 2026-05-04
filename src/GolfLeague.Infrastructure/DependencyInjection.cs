using Azure.Communication.Email;
using Azure.Identity;
using Azure.Storage.Blobs;
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
        var storageAccount = configuration["BLOB_STORAGE_ACCOUNT"]
            ?? throw new InvalidOperationException("BLOB_STORAGE_ACCOUNT is not configured.");
        var containerName = configuration["SQLITE_BLOB_CONTAINER"]
            ?? throw new InvalidOperationException("SQLITE_BLOB_CONTAINER is not configured.");
        var blobName = configuration["SQLITE_BLOB_NAME"]
            ?? throw new InvalidOperationException("SQLITE_BLOB_NAME is not configured.");

        var blobServiceUri = new Uri($"https://{storageAccount}.blob.core.windows.net");
        var credential = new DefaultAzureCredential();

        var blobServiceClient = new BlobServiceClient(blobServiceUri, credential);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        services.AddSingleton(blobServiceClient);
        services.AddSingleton(containerClient);

        var localDbPath = Path.Combine(Path.GetTempPath(), "golf-league", blobName);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={localDbPath}")
            .Options;

        services.AddSingleton(dbOptions);

        services.AddScoped<AppDbContext>(provider =>
        {
            var options = provider.GetRequiredService<DbContextOptions<AppDbContext>>();
            return new AppDbContext(options, containerClient, localDbPath, blobName);
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
