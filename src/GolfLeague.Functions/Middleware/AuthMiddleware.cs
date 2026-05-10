using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Functions.Middleware;

[ExcludeFromCodeCoverage]
public sealed class AuthMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(ILogger<AuthMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext is not null)
        {
            try
            {
                var authService = httpContext.RequestServices
                    .GetService(typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService))
                    as Microsoft.AspNetCore.Authentication.IAuthenticationService;

                if (authService is not null)
                {
                    var result = await authService.AuthenticateAsync(
                        httpContext,
                        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);

                    if (result.Succeeded && result.Principal is not null)
                    {
                        var principal = result.Principal;

                        // Roles come from Entra ID app roles (in the 'roles' claim of the JWT token)
                        // No need to add database roles - the token already contains the correct roles

                        httpContext.User = principal;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Authentication failed; proceeding as unauthenticated.");
            }
        }

        await next(context);
    }
}
