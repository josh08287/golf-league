using GolfLeague.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace GolfLeague.Functions.Middleware;

/// <summary>
/// Populates ILeagueContext for the duration of the request from the JWT claims.
/// The leagueId claim on the token is the sole source of league scope — no slug
/// header or database lookup required. Must run after AuthMiddleware so the
/// ClaimsPrincipal is already populated.
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
                var leagueIdClaim = user.FindFirst("leagueId")?.Value;
                if (int.TryParse(leagueIdClaim, out var parsedId))
                    leagueId = parsedId;

                leagueCtx.Set(leagueId, isSuperAdmin);
            }
        }

        await next(context);
    }
}
