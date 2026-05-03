using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses.Queries;

public sealed record GetCoursesQuery : IRequest<Result<PagedResult<CourseDto>>>;

public sealed class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, Result<PagedResult<CourseDto>>>
{
    private readonly ICourseRepository _courseRepository;

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

        return Result<PagedResult<CourseDto>>.Ok(new PagedResult<CourseDto>(dtos, 1, dtos.Count, dtos.Count));
    }
}
