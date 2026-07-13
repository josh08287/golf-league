using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

public sealed record ReopenRoundCommand(
    int RoundId,
    string UserId) : IRequest<Result<RoundDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class ReopenRoundCommandHandler : IRequestHandler<ReopenRoundCommand, Result<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IHandicapRepository _handicapRepository;

    public ReopenRoundCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository,
        IHandicapRepository handicapRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
        _handicapRepository = handicapRepository;
    }

    public async Task<Result<RoundDto>> Handle(ReopenRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<RoundDto>.Fail($"Round with ID {request.RoundId} not found.");

        if (round.Status != RoundStatus.Finalized)
            return Result<RoundDto>.Fail("Only finalized rounds can be re-opened.");

        var course = await _courseRepository.GetByIdAsync(round.CourseId, cancellationToken);
        if (course is null)
            return Result<RoundDto>.Fail($"Course with ID {round.CourseId} not found.");

        // Remove the calculated handicap records that were created when this round
        // was finalized so that re-finalizing produces a clean, correct recalculation.
        var participantPlayerIds = round.Participants
            .Where(p => !p.IsWithdrawn && !p.SkippedWeek && p.TotalGrossStrokes.HasValue)
            .Select(p => p.PlayerId)
            .Distinct();

        foreach (var playerId in participantPlayerIds)
            await _handicapRepository.DeleteCalculatedAsync(playerId, cancellationToken);

        await _roundRepository.UpdateStatusAsync(round.Id, RoundStatus.InProgress, cancellationToken);
        round.Status = RoundStatus.InProgress;

        return Result<RoundDto>.Ok(RoundDtoMapper.Map(round, course.Name, round.Participants.Count));
    }
}
