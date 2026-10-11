using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Sets whether a tournament round is a shotgun start (true: all groups tee
/// off simultaneously at the round's configured start time on different holes;
/// false: groups tee off 8 minutes apart starting on hole 1). Re-times existing
/// tee times to match.
/// </summary>
public sealed record SetTournamentShotgunCommand(
    int RoundId,
    bool IsShotgun,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentShotgunCommandHandler
    : IRequestHandler<SetTournamentShotgunCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;

    public SetTournamentShotgunCommandHandler(IRoundRepository roundRepository, ITeeTimeRepository teeTimeRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
    }

    public async Task<Result<bool>> Handle(SetTournamentShotgunCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<bool>.Fail("Shotgun start can only be changed before the round is finalized.");

        round.IsShotgun = request.IsShotgun;
        await _roundRepository.UpdateAsync(round, cancellationToken);
        await _teeTimeRepository.RetimeSlotsAsync(round.Id, round.FirstTeeTime, request.IsShotgun, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
