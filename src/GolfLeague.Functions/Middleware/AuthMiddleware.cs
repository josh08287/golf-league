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
                    if (principal.Identity is not null && principal.Identity.IsAuthenticated)
                    {
                        var entraObjectId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? principal.FindFirst("oid")?.Value
                            ?? principal.FindFirst("sub")?.Value;

                        if (!string.IsNullOrEmpty(entraObjectId))
                        {
                            var playerRepo = httpContext.RequestServices
                                .GetService(typeof(GolfLeague.Domain.Interfaces.IPlayerRepository))
                                as GolfLeague.Domain.Interfaces.IPlayerRepository;

                            if (playerRepo is not null)
                            {
                                var player = await playerRepo.GetByEntraObjectIdAsync(entraObjectId);
                                if (player is not null)
                                {
                                    var identity = principal.Identity as System.Security.Claims.ClaimsIdentity;
                                    if (identity is not null)
                                    {
                                        var roleClaim = new System.Security.Claims.Claim(
                                            System.Security.Claims.ClaimTypes.Role,
                                            player.Role.ToString().ToLowerInvariant());
                                        identity.AddClaim(roleClaim);
                                    }
                                }
                            }
                        }
                    }

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
