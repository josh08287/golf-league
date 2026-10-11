using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Application.Rounds;
using GolfLeague.Domain.Entities;
using GolfLeague.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Application.Courses.Commands;

// ── Update ────────────────────────────────────────────────────────────────────

public sealed record UpdateTeeBoxCommand(
    int CourseId,
    int TeeBoxId,
    string Name,
    double CourseRating,
    int SlopeRating,
    int TotalYardage,
    int Par,
    bool IsDefault,
    string UserId) : IRequest<Result<TeeBoxDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Course";
    public string AuditEntityId => CourseId.ToString();
}

/// <summary>
/// Edits a tee box. A rating/slope change is re-applied to every participant
/// on that tee in a round that isn't finalized or cancelled (and their entered
/// holes rescored); finalized rounds keep the rating they were played at.
/// </summary>
public sealed class UpdateTeeBoxCommandHandler : IRequestHandler<UpdateTeeBoxCommand, Result<TeeBoxDto>>
{
    private readonly ICourseRepository _courseRepository;
    private readonly IRoundRepository _roundRepository;
    private readonly ParticipantTeeUpdater _teeUpdater;
    private readonly ILogger<UpdateTeeBoxCommandHandler> _logger;

    public UpdateTeeBoxCommandHandler(
        ICourseRepository courseRepository,
        IRoundRepository roundRepository,
        ParticipantTeeUpdater teeUpdater,
        ILogger<UpdateTeeBoxCommandHandler> logger)
    {
        _courseRepository = courseRepository;
        _roundRepository = roundRepository;
        _teeUpdater = teeUpdater;
        _logger = logger;
    }

    public async Task<Result<TeeBoxDto>> Handle(UpdateTeeBoxCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<TeeBoxDto>.Fail("Tee box name is required.");
        if (request.SlopeRating <= 0 || request.CourseRating <= 0)
            return Result<TeeBoxDto>.Fail("Rating and slope must be positive.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<TeeBoxDto>.Fail("Course not found.");

        var existing = course.TeeBoxes.FirstOrDefault(t => t.Id == request.TeeBoxId);
        if (existing is null)
            return Result<TeeBoxDto>.Fail("Tee box not found on this course.");

        if (existing.IsDefault && !request.IsDefault)
            return Result<TeeBoxDto>.Fail("A course must have a default tee. Make another tee the default instead.");

        var ratingChanged = existing.CourseRating != request.CourseRating || existing.SlopeRating != request.SlopeRating;

        var updated = new TeeBox
        {
            Id = existing.Id,
            CourseId = existing.CourseId,
            Name = request.Name.Trim(),
            CourseRating = request.CourseRating,
            SlopeRating = request.SlopeRating,
            TotalYardage = request.TotalYardage,
            Par = request.Par,
            IsDefault = request.IsDefault,
        };
        await _courseRepository.UpdateTeeBoxAsync(updated, cancellationToken);

        if (ratingChanged)
        {
            var participants = await _roundRepository.GetOpenParticipantsByTeeBoxAsync(updated.Id, cancellationToken);
            foreach (var participant in participants)
            {
                await _teeUpdater.ApplyAsync(
                    participant, updated, participant.TeeBoxOverridden, participant.Round,
                    participant.HoleScores.ToList(), cancellationToken);
            }

            _logger.LogInformation(
                "User {UserId} changed tee box {TeeBoxId} rating/slope; re-applied to {Count} open participant(s)",
                request.UserId, updated.Id, participants.Count);
        }

        return Result<TeeBoxDto>.Ok(new TeeBoxDto(
            updated.Id, updated.Name, updated.CourseRating, updated.SlopeRating, updated.TotalYardage, updated.Par,
            existing.HoleTeeBoxes.Select(ht => new HoleTeeBoxDto(ht.CourseHoleId, ht.Yardage, ht.Par)).ToList(),
            updated.IsDefault));
    }
}

// ── Delete ────────────────────────────────────────────────────────────────────

public sealed record DeleteTeeBoxCommand(int CourseId, int TeeBoxId, string UserId)
    : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Course";
    public string AuditEntityId => CourseId.ToString();
}

public sealed class DeleteTeeBoxCommandHandler : IRequestHandler<DeleteTeeBoxCommand, Result<bool>>
{
    private readonly ICourseRepository _courseRepository;

    public DeleteTeeBoxCommandHandler(ICourseRepository courseRepository)
    {
        _courseRepository = courseRepository;
    }

    public async Task<Result<bool>> Handle(DeleteTeeBoxCommand request, CancellationToken cancellationToken)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Result<bool>.Fail("Course not found.");

        var teeBox = course.TeeBoxes.FirstOrDefault(t => t.Id == request.TeeBoxId);
        if (teeBox is null)
            return Result<bool>.Fail("Tee box not found on this course.");

        if (teeBox.IsDefault)
            return Result<bool>.Fail("The default tee can't be deleted. Make another tee the default first.");

        if (await _courseRepository.IsTeeBoxInUseAsync(teeBox.Id, cancellationToken))
            return Result<bool>.Fail("This tee has been used in a round and can't be deleted.");

        await _courseRepository.DeleteTeeBoxAsync(teeBox.Id, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
