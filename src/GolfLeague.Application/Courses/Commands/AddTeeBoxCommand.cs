using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Courses.Commands;

public sealed record AddTeeBoxCommand(
    int CourseId,
    string Name,
    double CourseRating,
    int SlopeRating,
    int TotalYardage,
    int Par,
    string UserId) : IRequest<Result<TeeBox>>;

public sealed class AddTeeBoxCommandHandler : IRequestHandler<AddTeeBoxCommand, Result<TeeBox>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<AddTeeBoxCommandHandler> _logger;

    public AddTeeBoxCommandHandler(ICourseRepository courseRepository, ILogger<AddTeeBoxCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<Result<TeeBox>> Handle(AddTeeBoxCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<TeeBox>.Fail("Course not found.");

        var teeBox = new TeeBox
        {
            CourseId = request.CourseId,
            Name = request.Name,
            CourseRating = request.CourseRating,
            SlopeRating = request.SlopeRating,
            TotalYardage = request.TotalYardage,
            Par = request.Par
        };

        await _courseRepository.AddTeeBoxAsync(teeBox, cancellationToken);

        _logger.LogInformation("User {UserId} added teebox {TeeBoxId} to course {CourseId}", request.UserId, teeBox.Id, request.CourseId);

        return Result<TeeBox>.Ok(teeBox);
    }
}
