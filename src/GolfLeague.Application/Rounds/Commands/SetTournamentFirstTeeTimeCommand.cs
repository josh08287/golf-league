using GolfLeague.Application.Common;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Rounds.Commands;

/// <summary>
/// Sets a tournament round's first tee time (null resets to the default
/// start) and re-times every existing tee time to match, keeping the usual
/// interval between groups.
/// </summary>
public sealed record SetTournamentFirstTeeTimeCommand(
    int RoundId,
    TimeOnly? FirstTeeTime,
    string UserId) : IRequest<Result<bool>>, IAmAuditableCommand
{
    public string AuditEntityType => "Round";
    public string AuditEntityId => RoundId.ToString();
}

public sealed class SetTournamentFirstTeeTimeCommandHandler
    : IRequestHandler<SetTournamentFirstTeeTimeCommand, Result<bool>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ITeeTimeRepository _teeTimeRepository;

    public SetTournamentFirstTeeTimeCommandHandler(IRoundRepository roundRepository, ITeeTimeRepository teeTimeRepository)
    {
        _roundRepository = roundRepository;
        _teeTimeRepository = teeTimeRepository;
    }

    public async Task<Result<bool>> Handle(SetTournamentFirstTeeTimeCommand request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return Result<bool>.Fail($"Round {request.RoundId} not found.");
        if (round.RoundType != RoundType.Tournament)
            return Result<bool>.Fail("This round is not a tournament round.");
        if (round.Status == RoundStatus.Finalized)
            return Result<bool>.Fail("The start time can only be changed before the round is finalized.");

        round.FirstTeeTime = request.FirstTeeTime;
        await _roundRepository.UpdateAsync(round, cancellationToken);
        await _teeTimeRepository.RetimeSlotsAsync(round.Id, request.FirstTeeTime, cancellationToken);

        return Result<bool>.Ok(true);
    }
}
