using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Middleware;

/// <summary>
/// Catches exceptions that escape a function body and turns them into a structured
/// 500 response instead of a bare host-level failure, so clients get consistent JSON
/// and every occurrence is logged with the operation id for correlation in App Insights.
/// Must run outermost (registered first) so it wraps auth/league-context middleware too.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception in function {FunctionName} (invocation {InvocationId}).",
                context.FunctionDefinition.Name, context.InvocationId);

            var httpContext = context.GetHttpContext();
            if (httpContext is not null && !httpContext.Response.HasStarted)
            {
                httpContext.Response.Clear();
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "An unexpected error occurred.",
                    invocationId = context.InvocationId,
                }));
            }
        }
    }
}
