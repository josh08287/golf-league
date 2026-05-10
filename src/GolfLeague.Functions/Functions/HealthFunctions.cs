using GolfLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminMigrateFunction> _logger;

    public AdminMigrateFunction(AppDbContext dbContext, ILogger<AdminMigrateFunction> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Function("MigrateDatabase")]
    public async Task<IActionResult> MigrateDatabase(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/migrate")] HttpRequest req)
    {
        try
        {
            var pending = (await _dbContext.Database.GetPendingMigrationsAsync()).ToList();
            var applied = (await _dbContext.Database.GetAppliedMigrationsAsync()).ToList();

            _logger.LogInformation("Migrate: {AppliedCount} applied, {PendingCount} pending.", applied.Count, pending.Count);

            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await _dbContext.Database.MigrateAsync();
            });

            var afterApplied = (await _dbContext.Database.GetAppliedMigrationsAsync()).ToList();
            return new OkObjectResult(new
            {
                ok = true,
                appliedBefore = applied,
                pendingBefore = pending,
                appliedAfter = afterApplied,
            });
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
