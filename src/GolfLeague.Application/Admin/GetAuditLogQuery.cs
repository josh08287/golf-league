using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Admin;

public sealed record AuditLogEntryDto(
    int Id,
    string Timestamp,
    string Action,
    string EntityType,
    string EntityId,
    string UserId,
    string? Details);

public sealed record AuditLogPageDto(
    List<AuditLogEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GetAuditLogQuery(int Page, int PageSize, SortRequest? Sort = null)
    : IRequest<Result<AuditLogPageDto>>;

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, Result<AuditLogPageDto>>
{
    private readonly IAuditRepository _auditRepository;

    /// <summary>
    /// Default sort: newest entry first (matches the repo's existing default).
    /// </summary>
    private static readonly SortMap<AuditLogEntryDto> SortMap = new SortMap<AuditLogEntryDto>(
            source => source.OrderByDescending(a => a.Timestamp))
        .Add("timestamp", a => a.Timestamp)
        .Add("action", a => a.Action)
        .Add("entityType", a => a.EntityType)
        .Add("entityId", a => a.EntityId)
        .Add("userId", a => a.UserId);

    public GetAuditLogQueryHandler(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<Result<AuditLogPageDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        // The repo paginates server-side already, but to support arbitrary
        // sort columns we need the full set in memory. League scale (audit
        // entries grow ~slow) makes this fine; if it becomes a problem we'd
        // push ORDER BY into SQL via a per-column expression map.
        var (items, totalCount) = await _auditRepository.GetPagedAsync(1, int.MaxValue, cancellationToken);

        var dtos = items.Select(a => new AuditLogEntryDto(
            a.Id,
            a.Timestamp.ToString("O"),
            a.Action,
            a.EntityType,
            a.EntityId,
            a.UserId,
            a.AfterJson
        )).ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        var paged = sorted
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return Result<AuditLogPageDto>.Ok(new AuditLogPageDto(paged, totalCount, request.Page, request.PageSize));
    }
}
