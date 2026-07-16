using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Statistics.Queries;

public sealed record GetCoursesWithDataQuery(int? SeasonId = null, int? HalfId = null)
    : IRequest<Result<List<int>>>;

public sealed class GetCoursesWithDataQueryHandler
    : IRequestHandler<GetCoursesWithDataQuery, Result<List<int>>>
{
    private readonly IRoundRepository _roundRepository;

    public GetCoursesWithDataQueryHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<List<int>>> Handle(
        GetCoursesWithDataQuery request,
        CancellationToken cancellationToken)
    {
        var rounds = request.HalfId is int halfId
            ? await _roundRepository.GetByHalfAsync(halfId, cancellationToken)
            : request.SeasonId is int seasonId
                ? await _roundRepository.GetBySeasonAsync(seasonId, cancellationToken)
                : await _roundRepository.GetAllAsync(cancellationToken);

        var courseIds = rounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .Select(r => r.CourseId)
            .Distinct()
            .ToList();

        return Result<List<int>>.Ok(courseIds);
    }
}
