using GolfLeague.Application.Interfaces;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace GolfLeague.Functions.Middleware;

/// <summary>
/// Populates ILeagueContext for the duration of the request.
/// Priority: JWT leagueId claim (authenticated) → X-League-Slug header (anonymous).
/// Must run after AuthMiddleware so the ClaimsPrincipal is already populated.
/// </summary>
public sealed class LeagueContextMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is not null)
        {
            var leagueCtx = httpContext.RequestServices.GetService<ILeagueContext>();
            if (leagueCtx is not null)
            {
                var user = httpContext.User;
                var isSuperAdmin = user.FindFirst("superAdmin")?.Value == "true";

                int? leagueId = null;

                // Prefer the leagueId baked into the JWT.
                var leagueIdRaw = user.FindFirst("leagueId")?.Value;
                if (int.TryParse(leagueIdRaw, out var lid))
                {
                    leagueId = lid;
                }
                else
                {
                    // Fall back to the X-League-Slug header sent by the frontend
                    // for anonymous (unauthenticated) requests.
                    var slugHeader = httpContext.Request.Headers["X-League-Slug"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(slugHeader))
                    {
                        var leagueRepo = httpContext.RequestServices.GetService<ILeagueRepository>();
                        if (leagueRepo is not null)
                        {
                            var league = await leagueRepo.GetBySlugAsync(slugHeader, CancellationToken.None);
                            leagueId = league?.Id;
                        }
                    }
                }

                leagueCtx.Set(leagueId, isSuperAdmin);
            }
        }

        await next(context);
    }
}
