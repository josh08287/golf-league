using GolfLeague.Application.Common;
using GolfLeague.Application.Statistics;
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
    private readonly IFlightRepository _flightRepository;

    public GetCoursesWithDataQueryHandler(IRoundRepository roundRepository, IFlightRepository flightRepository)
    {
        _roundRepository = roundRepository;
        _flightRepository = flightRepository;
    }

    public async Task<Result<List<int>>> Handle(
        GetCoursesWithDataQuery request,
        CancellationToken cancellationToken)
    {
        // Includes tournament rounds (which have no half), so a course that has
        // only hosted tournaments still shows up.
        var rounds = await StatisticsPeriodRounds.GetAsync(
            _roundRepository, _flightRepository, request.SeasonId, request.HalfId, cancellationToken);

        var courseIds = rounds
            .Where(r => r.Status == RoundStatus.Finalized)
            .Select(r => r.CourseId)
            .Distinct()
            .ToList();

        return Result<List<int>>.Ok(courseIds);
    }
}
