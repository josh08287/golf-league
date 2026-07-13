using GolfLeague.Application.Common;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Courses.Commands;

public sealed record HoleTeeBoxInput(int CourseHoleId, int Yardage, int Par);

public sealed record UpdateHoleTeeBoxesCommand(
    int CourseId,
    int TeeBoxId,
    List<HoleTeeBoxInput> Holes,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Course";
    public string AuditEntityId => CourseId.ToString();
}

public sealed class UpdateHoleTeeBoxesCommandHandler : IRequestHandler<UpdateHoleTeeBoxesCommand, Result<bool>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly ILogger<UpdateHoleTeeBoxesCommandHandler> _logger;

    public UpdateHoleTeeBoxesCommandHandler(ICourseRepository courseRepository, ILogger<UpdateHoleTeeBoxesCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(UpdateHoleTeeBoxesCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<bool>.Fail("Course not found.");

        var teeBox = course.TeeBoxes.FirstOrDefault(t => t.Id == request.TeeBoxId);
        if (teeBox is null)
            return Result<bool>.Fail("TeeBox not found on this course.");

        var newHoleTeeBoxes = request.Holes.Select(h => new HoleTeeBox
        {
            TeeBoxId = request.TeeBoxId,
            CourseHoleId = h.CourseHoleId,
            Yardage = h.Yardage,
            Par = h.Par
        }).ToList();

        await _courseRepository.UpdateHoleTeeBoxesAsync(request.TeeBoxId, newHoleTeeBoxes, cancellationToken);

        _logger.LogInformation("User {UserId} updated hole teeboxes for TeeBox {TeeBoxId}", request.UserId, request.TeeBoxId);

        return Result<bool>.Ok(true);
    }
}
