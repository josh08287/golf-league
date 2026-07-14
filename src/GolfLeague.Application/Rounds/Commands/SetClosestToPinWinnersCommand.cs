using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>One closest-to-pin selection: null PlayerId means "None" (no winner for that hole).</summary>
public sealed record ClosestToPinSelection(int HoleNumber, int? PlayerId);

/// <summary>
/// Replaces the round's closest-to-pin winners with the given selections.
/// Holes omitted from <paramref name="Selections"/> (or given a null PlayerId)
/// end up with no winner recorded.
/// </summary>
public sealed record SetClosestToPinWinnersCommand(
    int RoundId,
    List<ClosestToPinSelection> Selections,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetClosestToPinWinnersCommandHandler
    : IRequestHandler<SetClosestToPinWinnersCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ICourseRepository _courseRepository;

    public SetClosestToPinWinnersCommandHandler(
        IRoundRepository roundRepository,
        ICourseRepository courseRepository)
    {
        _roundRepository = roundRepository;
        _courseRepository = courseRepository;
    }

    public async Task<Result<bool>> Handle(SetClosestToPinWinnersCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.Status == RoundStatus.Finalized || round.Status == RoundStatus.Cancelled)
            return Result<bool>.Fail($"Cannot change closest-to-pin winners on a round with status '{round.Status}'.");

        var winners = request.Selections
            .Where(s => s.PlayerId.HasValue)
            .Select(s => (s.HoleNumber, PlayerId: s.PlayerId!.Value))
            .ToList();

        if (winners.Count > 0)
        {
            // Every selected hole must be a par 3 on the round's nine.
            var allHoles = await _courseRepository.GetHolesAsync(round.CourseId, cancellationToken);
            var par3HoleNumbers = (round.NineHoleSide == NineHoleSide.Back
                    ? allHoles.Where(h => h.HoleNumber >= 10)
                    : allHoles.Where(h => h.HoleNumber <= 9))
                .Where(h => h.Par == 3)
                .Select(h => h.HoleNumber)
                .ToHashSet();

            var invalidHole = winners
                .Where(w => !par3HoleNumbers.Contains(w.HoleNumber))
                .Select(w => (int?)w.HoleNumber)
                .FirstOrDefault();
            if (invalidHole is not null)
                return Result<bool>.Fail($"Hole {invalidHole} is not a par 3 on this round.");

            // Every selected winner must be an active participant of the round.
            var participants = await _roundRepository.GetParticipantsAsync(request.RoundId, cancellationToken);
            var eligiblePlayerIds = participants
                .Where(p => !p.IsWithdrawn && !p.SkippedWeek && !p.IsSubstitute)
                .Select(p => p.PlayerId)
                .ToHashSet();

            var invalidPlayer = winners
                .Where(w => !eligiblePlayerIds.Contains(w.PlayerId))
                .Select(w => (int?)w.PlayerId)
                .FirstOrDefault();
            if (invalidPlayer is not null)
                return Result<bool>.Fail($"Player {invalidPlayer} is not an active participant in round {request.RoundId}.");
        }

        await _roundRepository.SetClosestToPinWinnersAsync(request.RoundId, winners, cancellationToken);
        return Result<bool>.Ok(true);
    }
}
