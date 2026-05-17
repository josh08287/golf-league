using GolfLeague.Application.Admin;
using GolfLeague.Application.Common;
using GolfLeague.Functions.Helpers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace GolfLeague.Functions.Functions;

public sealed class AdminFunctions
{
    private readonly IMediator _mediator;

    public AdminFunctions(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("GetAuditLog")]
    public async Task<IActionResult> GetAuditLog(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/audit-log")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireSuperAdmin();
        if (authError is not null) return authError;

        int.TryParse(req.Query["page"], out var page);
        int.TryParse(req.Query["pageSize"], out var pageSize);
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        var sort = SortRequest.TryParse(req.Query["sortBy"], req.Query["sortDir"]);

        var result = await _mediator.Send(new GetAuditLogQuery(page, pageSize, sort), cancellationToken);
        return result.ToOkResult();
    }

    [Function("RecalculateAllHandicaps")]
    public async Task<IActionResult> RecalculateAllHandicaps(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/handicaps/recalculate")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new RecalculateAllHandicapsCommand(userId), cancellationToken);
        return result.ToOkResult();
    }

    [Function("RecalculateAllRounds")]
    public async Task<IActionResult> RecalculateAllRounds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/rounds/recalculate")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        var userId = req.GetUserId() ?? "unknown";
        var result = await _mediator.Send(new RecalculateAllRoundsCommand(userId), cancellationToken);
        return result.ToOkResult();
    }
}
