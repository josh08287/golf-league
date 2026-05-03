using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Queries;

public sealed record GetRoundsQuery(int? SeasonId, int Page = 1, int PageSize = 20) : IRequest<Result<PagedResult<RoundDto>>>;

public sealed class GetRoundsQueryHandler : IRequestHandler<GetRoundsQuery, Result<PagedResult<RoundDto>>>
{
    private readonly IRoundRepository _roundRepository;

    public GetRoundsQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<PagedResult<RoundDto>>> Handle(GetRoundsQuery request, CancellationToken cancellationToken)
    {
        var rounds = request.SeasonId.HasValue
            ? await _roundRepository.GetBySeasonAsync(request.SeasonId.Value, cancellationToken)
            : await _roundRepository.GetAllAsync(cancellationToken);

        var totalCount = rounds.Count;
        var paged = rounds
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtos = paged.Select(r => new RoundDto(
            r.Id,
            r.SeasonId,
            r.FlightId,
            r.Flight?.Name ?? string.Empty,
            r.CourseId,
            r.Course?.Name ?? string.Empty,
            r.RoundDate,
            r.Status,
            r.Participants.Count
        )).ToList();

        return Result<PagedResult<RoundDto>>.Ok(new PagedResult<RoundDto>(dtos, request.Page, request.PageSize, totalCount));
    }
}
