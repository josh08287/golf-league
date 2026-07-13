using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses.Commands;

public sealed record HoleInput(int HoleNumber, int Par, int StrokeIndex);

public sealed record UpdateCourseHolesCommand(
    int CourseId,
    List<HoleInput> Holes,
    string UserId) : IRequest<Result<CourseDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Course";
    public string AuditEntityId => CourseId.ToString();
}

public sealed class UpdateCourseHolesCommandHandler : IRequestHandler<UpdateCourseHolesCommand, Result<CourseDto>>
{
    private readonly ICourseRepository _courseRepository;

    public UpdateCourseHolesCommandHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<CourseDto>> Handle(UpdateCourseHolesCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<CourseDto>.Fail($"Course with ID {request.CourseId} not found.");

        var holes = request.Holes.Select(h => new CourseHole
        {
            CourseId = request.CourseId,
            HoleNumber = h.HoleNumber,
            Par = h.Par,
            StrokeIndex = h.StrokeIndex,
        }).ToList();

        await _courseRepository.UpdateHolesAsync(request.CourseId, holes, cancellationToken);

        var holeDtos = holes.Select(h => new CourseHoleDto(h.Id, h.HoleNumber, h.Par, h.StrokeIndex)).ToList();
        var dto = new CourseDto(course.Id, course.Name, course.CourseRating, course.SlopeRating, holes.Count, holeDtos);
        return Result<CourseDto>.Ok(dto);
    }
}
