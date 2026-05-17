using GolfLeague.Application.Interfaces;
using GolfLeague.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace GolfLeague.Functions.Middleware;

/// <summary>
/// Reads the leagueId and superAdmin claims from the authenticated JWT and
/// populates ILeagueContext for the duration of the request. Must run after
/// AuthMiddleware so the ClaimsPrincipal is already populated.
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
                var leagueIdRaw = user.FindFirst("leagueId")?.Value;
                if (int.TryParse(leagueIdRaw, out var lid))
                    leagueId = lid;

                leagueCtx.Set(leagueId, isSuperAdmin);
            }
        }

        await next(context);
    }
}
