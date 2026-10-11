using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses.Commands;

public sealed record CreateCourseCommand(
    string Name,
    double Rating,
    int Slope,
    string UserId) : IRequest<Result<CourseDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Course";
    public string AuditEntityId => "0"; // assigned by the DB; resolved from the response
}

public sealed class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Result<CourseDto>>
{
    private readonly ICourseRepository _courseRepository;

    public CreateCourseCommandHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<CourseDto>> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        var course = new Course
        {
            Name = request.Name,
            CourseRating = request.Rating,
            SlopeRating = request.Slope,
        };

        // Every course has a default tee; players with no assigned tee play
        // from it. Seeded from the rating/slope entered for the course.
        course.TeeBoxes.Add(new TeeBox
        {
            Name = "Default",
            CourseRating = request.Rating,
            SlopeRating = request.Slope,
            IsDefault = true,
        });

        await _courseRepository.AddAsync(course, cancellationToken);

        var dto = new CourseDto(course.Id, course.Name, course.CourseRating, course.SlopeRating, 0, []);
        return Result<CourseDto>.Ok(dto);
    }
}
