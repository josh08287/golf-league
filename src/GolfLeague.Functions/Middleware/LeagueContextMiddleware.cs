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
/// The X-League-Slug header (always sent by the frontend, derived from the URL path)
/// is the sole source of which league's data to return. The JWT leagueId claim is
/// intentionally ignored here — it only reflects the league the token was issued for,
/// not the league the user is currently viewing. IsSuperAdmin still comes from the JWT.
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

                // Always resolve league from the X-League-Slug header so the data
                // scope follows the URL the user is viewing, not the JWT's leagueId.
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

                leagueCtx.Set(leagueId, isSuperAdmin);
            }
        }

        await next(context);
    }
}
