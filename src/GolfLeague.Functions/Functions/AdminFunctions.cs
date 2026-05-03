using GolfLeague.Application.Admin;
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/audit-log")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var authError = req.RequireRole("admin");
        if (authError is not null) return authError;

        int.TryParse(req.Query["page"], out var page);
        int.TryParse(req.Query["pageSize"], out var pageSize);
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;

        var result = await _mediator.Send(new GetAuditLogQuery(page, pageSize), cancellationToken);
        return result.ToOkResult();
    }
}
