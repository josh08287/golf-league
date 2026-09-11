using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Functions;

public sealed class HealthFunctions
{
    [Function("Health")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }
}

public sealed class AdminMigrateFunction
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AdminMigrateFunction> _logger;

    public AdminMigrateFunction(IServiceProvider services, ILogger<AdminMigrateFunction> logger)
    {
        _services = services;
        _logger = logger;
    }

    // Applies pending migrations and runs role/league/season/admin seeding on demand.
    // Called once per deploy by the CI/CD workflow instead of running this on every
    // Function host cold start, which would otherwise wake the paused SQL Serverless DB
    // far more often than real deploys happen.
    [Function("MigrateDatabase")]
    public async Task<IActionResult> MigrateDatabase(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/migrate")] HttpRequest req)
    {
        try
        {
            await DatabaseInitializer.EnsureDatabaseInitializedAsync(_services, _logger);
            return new OkObjectResult(new { ok = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migrate: failed.");
            return new ObjectResult(new
            {
                ok = false,
                error = ex.GetType().FullName,
                message = ex.Message,
                inner = ex.InnerException?.Message,
                stack = ex.ToString(),
            })
            { StatusCode = 500 };
        }
    }
}
