using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Opts a tournament round's 18-hole scores into (or out of) players'
/// handicaps. Locked once the round is finalized, because finalizing is when
/// handicaps are recalculated — re-open the round to change it.
/// </summary>
public sealed record SetTournamentCountsTowardHandicapCommand(
    int RoundId,
    bool CountsTowardHandicap,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentCountsTowardHandicapCommandHandler
    : IRequestHandler<SetTournamentCountsTowardHandicapCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;

    public SetTournamentCountsTowardHandicapCommandHandler(IRoundRepository roundRepository)
    {
        _roundRepository = roundRepository;
    }

    public async Task<Result<bool>> Handle(SetTournamentCountsTowardHandicapCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<bool>.Fail("Whether this round counts toward handicaps can only be changed before it's finalized. Re-open the round to change it.");

        round.CountsTowardHandicap = request.CountsTowardHandicap;
        await _roundRepository.UpdateAsync(round, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
