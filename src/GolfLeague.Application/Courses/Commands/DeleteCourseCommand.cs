using GolfLeague.Application.Common;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Courses.Commands;

public sealed record DeleteCourseCommand(
    int Id,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand;

public sealed class DeleteCourseCommandHandler : IRequestHandler<DeleteCourseCommand, Result<bool>>
{
    private readonly ICourseRepository _courseRepository;

    public DeleteCourseCommandHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<bool>> Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (course is null)
            return Result<bool>.Fail($"Course with ID {request.Id} not found.");

        await _courseRepository.DeleteAsync(request.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
