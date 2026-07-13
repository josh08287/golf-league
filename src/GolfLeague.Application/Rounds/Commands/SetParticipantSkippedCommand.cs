using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Marks a round participant as having skipped the week (or unmarks them).
/// Skipped players score 0 Stableford points for the round and are excluded
/// from handicap differential calculations. When a player is marked as
/// skipped, any previously-entered hole scores are cleared and totals are
/// reset to 0 so the round shows as a played-but-zeroed week in standings.
/// </summary>
public sealed record SetParticipantSkippedCommand(
    int RoundId,
    int PlayerId,
    bool Skipped,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetParticipantSkippedCommandHandler
    : IRequestHandler<SetParticipantSkippedCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;

    public SetParticipantSkippedCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<bool>> Handle(SetParticipantSkippedCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round with ID {request.RoundId} not found.");

        if (round.Status == RoundStatus.Finalized || round.Status == RoundStatus.Cancelled)
            return Result<bool>.Fail($"Cannot change skip status on a round with status '{round.Status}'.");

        var participant = await _roundRepository.GetParticipantAsync(request.RoundId, request.PlayerId, cancellationToken);
        if (participant is null)
            return Result<bool>.Fail($"Player {request.PlayerId} is not a participant in round {request.RoundId}.");

        participant.SkippedWeek = request.Skipped;

        if (request.Skipped)
        {
            // Wipe any partial entry: skipped means zero, not whatever was typed.
            await _roundRepository.ClearHoleScoresAsync(participant.Id, cancellationToken);
            participant.TotalGrossStrokes = 0;
            participant.TotalNetStrokes = 0;
            participant.TotalGrossStablefordPoints = 0;
            participant.TotalNetStablefordPoints = 0;
        }
        else
        {
            // Unskipping resets totals to null so they don't appear as a 0
            // until real scores are entered.
            participant.TotalGrossStrokes = null;
            participant.TotalNetStrokes = null;
            participant.TotalGrossStablefordPoints = null;
            participant.TotalNetStablefordPoints = null;
        }

        await _roundRepository.UpdateParticipantAsync(participant, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
