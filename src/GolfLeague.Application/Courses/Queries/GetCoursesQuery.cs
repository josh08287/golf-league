using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses.Queries;

public sealed record GetCoursesQuery(SortRequest? Sort = null) : IRequest<Result<PagedResult<CourseDto>>>;

public sealed class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, Result<PagedResult<CourseDto>>>
{
    private readonly ICourseRepository _courseRepository;

    /// <summary>
    /// Default sort: course name ascending.
    /// </summary>
    private static readonly SortMap<CourseDto> SortMap = new SortMap<CourseDto>(
            source => source.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        .Add("name", c => c.Name)
        .Add("rating", c => c.Rating)
        .Add("courseRating", c => c.Rating)
        .Add("slope", c => c.Slope)
        .Add("slopeRating", c => c.Slope)
        .Add("holes", c => c.HoleCount)
        .Add("holeCount", c => c.HoleCount);

    public GetCoursesQueryHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<PagedResult<CourseDto>>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        var courses = await _courseRepository.GetAllAsync(cancellationToken);

        var dtos = courses.Select(c => new CourseDto(
            c.Id,
            c.Name,
            c.CourseRating,
            c.SlopeRating,
            c.Holes.Count,
            c.Holes.OrderBy(h => h.HoleNumber).Select(h => new CourseHoleDto(
                h.HoleNumber,
                h.Par,
                h.StrokeIndex)).ToList()
        )).ToList();

        var sorted = SortMap.Apply(dtos, request.Sort);
        return Result<PagedResult<CourseDto>>.Ok(new PagedResult<CourseDto>(sorted.ToList(), 1, sorted.Count, sorted.Count));
    }
}
