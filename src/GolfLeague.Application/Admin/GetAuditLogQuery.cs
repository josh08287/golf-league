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

public sealed record GetAuditLogQuery(int Page, int PageSize) : IRequest<Result<AuditLogPageDto>>;

public sealed class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, Result<AuditLogPageDto>>
{
    private readonly IAuditRepository _auditRepository;

    public GetAuditLogQueryHandler(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<Result<AuditLogPageDto>> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _auditRepository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);

        var dtos = items.Select(a => new AuditLogEntryDto(
            a.Id,
            a.Timestamp.ToString("O"),
            a.Action,
            a.EntityType,
            a.EntityId,
            a.UserId,
            a.AfterJson
        )).ToList();

        return Result<AuditLogPageDto>.Ok(new AuditLogPageDto(dtos, totalCount, request.Page, request.PageSize));
    }
}
