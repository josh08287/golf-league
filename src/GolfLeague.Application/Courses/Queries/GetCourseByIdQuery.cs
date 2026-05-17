using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses.Queries;

public sealed record GetCourseByIdQuery(int CourseId) : IRequest<Result<CourseDto>>;

public sealed class GetCourseByIdQueryHandler : IRequestHandler<GetCourseByIdQuery, Result<CourseDto>>
{
    private readonly ICourseRepository _courseRepository;

    public GetCourseByIdQueryHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<CourseDto>> Handle(GetCourseByIdQuery request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<CourseDto>.Fail($"Course with ID {request.CourseId} not found.");

        var holeDtos = course.Holes
            .OrderBy(h => h.HoleNumber)
            .Select(h => new CourseHoleDto(h.Id, h.HoleNumber, h.Par, h.StrokeIndex))
            .ToList();

        var teeBoxDtos = course.TeeBoxes
            .OrderBy(t => t.TotalYardage) // Longest/Shortest doesn't matter much, let's just sort
            .Select(t => new TeeBoxDto(
                t.Id,
                t.Name,
                t.CourseRating,
                t.SlopeRating,
                t.TotalYardage,
                t.Par,
                t.HoleTeeBoxes.Select(ht => new HoleTeeBoxDto(ht.CourseHoleId, ht.Yardage, ht.Par)).ToList()
            )).ToList();

        var dto = new CourseDto(course.Id, course.Name, course.CourseRating, course.SlopeRating, holeDtos.Count, holeDtos, teeBoxDtos);
        return Result<CourseDto>.Ok(dto);
    }
}
